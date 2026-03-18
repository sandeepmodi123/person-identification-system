using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Infrastructure;
using PersonIdentificationSystem.API.Models.Entities;
using PersonIdentificationSystem.API.Repositories;

namespace PersonIdentificationSystem.API.Services;

public interface IPersonService
{
    Task<PagedResult<PersonDto>> GetPersonsAsync(int page, int pageSize, string? search, string? riskLevel, bool? isActive, CancellationToken ct = default);
    Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken ct = default);
    Task<PersonDto> CreatePersonAsync(CreatePersonRequest request, CancellationToken ct = default);
    Task<PersonDto?> UpdatePersonAsync(Guid id, UpdatePersonRequest request, CancellationToken ct = default);
    Task<bool> DeletePersonAsync(Guid id, CancellationToken ct = default);
    Task<PersonPhotoDto> AddPhotoAsync(Guid personId, IFormFile photo, bool isPrimary, CancellationToken ct = default);
    Task<List<PersonPhotoDto>> GetPhotosAsync(Guid personId, CancellationToken ct = default);
    Task<bool> DeletePhotoAsync(Guid personId, Guid photoId, CancellationToken ct = default);
}

public class PersonService : IPersonService
{
    private readonly IPersonRepository _personRepo;
    private readonly IRepository<PersonPhoto> _photoRepo;
    private readonly IPythonFaceRecognitionClient _pythonClient;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PersonService> _logger;
    private readonly IConfiguration _config;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png" };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public PersonService(
        IPersonRepository personRepo,
        IRepository<PersonPhoto> photoRepo,
        IPythonFaceRecognitionClient pythonClient,
        IWebHostEnvironment env,
        ILogger<PersonService> logger,
        IConfiguration config)
    {
        _personRepo = personRepo;
        _photoRepo = photoRepo;
        _pythonClient = pythonClient;
        _env = env;
        _logger = logger;
        _config = config;
    }

    public async Task<PagedResult<PersonDto>> GetPersonsAsync(
        int page, int pageSize, string? search, string? riskLevel, bool? isActive, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, total) = await _personRepo.GetPagedAsync(page, pageSize, search, riskLevel, isActive, ct);
        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<PersonDto>(dtos, total, page, pageSize, (int)Math.Ceiling((double)total / pageSize));
    }

    public async Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken ct = default)
    {
        var person = await _personRepo.GetWithPhotosAsync(id, ct);
        return person is null ? null : MapToDto(person);
    }

    public async Task<PersonDto> CreatePersonAsync(CreatePersonRequest request, CancellationToken ct = default)
    {
        var person = new Person
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            RiskLevel = request.RiskLevel,
            IsActive = request.IsActive
        };
        await _personRepo.AddAsync(person, ct);
        _logger.LogInformation("Created person {PersonId} - {Name}", person.Id, person.Name);
        return MapToDto(person);
    }

    public async Task<PersonDto?> UpdatePersonAsync(Guid id, UpdatePersonRequest request, CancellationToken ct = default)
    {
        var person = await _personRepo.GetWithPhotosAsync(id, ct);
        if (person is null) return null;

        if (request.Name is not null) person.Name = request.Name.Trim();
        if (request.Description is not null) person.Description = request.Description.Trim();
        if (request.RiskLevel is not null) person.RiskLevel = request.RiskLevel;
        if (request.IsActive.HasValue) person.IsActive = request.IsActive.Value;
        person.DateUpdated = DateTime.UtcNow;

        await _personRepo.UpdateAsync(person, ct);
        return MapToDto(person);
    }

    public async Task<bool> DeletePersonAsync(Guid id, CancellationToken ct = default)
    {
        var person = await _personRepo.GetByIdAsync(id, ct);
        if (person is null) return false;
        await _personRepo.DeleteAsync(person, ct);
        return true;
    }

    public async Task<PersonPhotoDto> AddPhotoAsync(Guid personId, IFormFile photo, bool isPrimary, CancellationToken ct = default)
    {
        // Validate file
        if (photo.Length > MaxFileSizeBytes)
            throw new ArgumentException($"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB.");

        var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"File type '{ext}' not allowed. Use: {string.Join(", ", AllowedExtensions)}");

        var person = await _personRepo.GetByIdAsync(personId, ct)
            ?? throw new KeyNotFoundException($"Person {personId} not found.");

        // Save file
        var uploadPath = Path.Combine(_config["UploadBasePath"] ?? "uploads", "persons", personId.ToString());
        Directory.CreateDirectory(uploadPath);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadPath, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await photo.CopyToAsync(stream, ct);

        var photoEntity = new PersonPhoto
        {
            PersonId = personId,
            PhotoUrl = $"/uploads/persons/{personId}/{fileName}",
            IsPrimary = isPrimary,
            FileSizeBytes = photo.Length,
            OriginalFilename = Path.GetFileName(photo.FileName)
        };

        await _photoRepo.AddAsync(photoEntity, ct);
        _logger.LogInformation("Added photo {PhotoId} to person {PersonId}", photoEntity.Id, personId);

        // Register face embedding with the Python face recognition service
        try
        {
            var photoBytes = await File.ReadAllBytesAsync(filePath, ct);
            var imageBase64 = Convert.ToBase64String(photoBytes);
            var registered = await _pythonClient.RegisterFaceAsync(personId, person.Name, imageBase64, ct);
            if (!registered)
                _logger.LogWarning("Failed to register face embedding for person {PersonId}", personId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering face embedding for person {PersonId}", personId);
        }

        return MapPhotoToDto(photoEntity);
    }

    public async Task<List<PersonPhotoDto>> GetPhotosAsync(Guid personId, CancellationToken ct = default)
    {
        var person = await _personRepo.GetWithPhotosAsync(personId, ct);
        return person?.Photos.Select(MapPhotoToDto).ToList() ?? new List<PersonPhotoDto>();
    }

    public async Task<bool> DeletePhotoAsync(Guid personId, Guid photoId, CancellationToken ct = default)
    {
        var photo = await _photoRepo.GetByIdAsync(photoId, ct);
        if (photo is null || photo.PersonId != personId) return false;
        await _photoRepo.DeleteAsync(photo, ct);
        return true;
    }

    private static PersonDto MapToDto(Person p) => new(
        p.Id, p.Name, p.Description, p.RiskLevel, p.IsActive, p.DateAdded,
        p.Photos.Select(MapPhotoToDto).ToList());

    private static PersonPhotoDto MapPhotoToDto(PersonPhoto ph) => new(
        ph.Id, ph.PersonId, ph.PhotoUrl, ph.QualityScore, ph.IsPrimary, ph.UploadDate, ph.OriginalFilename);
}
