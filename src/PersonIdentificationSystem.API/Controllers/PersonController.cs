using Microsoft.AspNetCore.Mvc;
using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Services;

namespace PersonIdentificationSystem.API.Controllers;

/// <summary>
/// Manages person profiles and their associated photos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PersonController : ControllerBase
{
    private readonly IPersonService _personService;

    public PersonController(IPersonService personService)
    {
        _personService = personService;
    }

    /// <summary>Get paginated list of persons.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PersonDto>), 200)]
    public async Task<ActionResult<PagedResult<PersonDto>>> GetPersons(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? riskLevel = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        return Ok(await _personService.GetPersonsAsync(page, pageSize, search, riskLevel, isActive, ct));
    }

    /// <summary>Get a person by ID with photos.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PersonDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<PersonDto>> GetPerson(Guid id, CancellationToken ct = default)
    {
        var person = await _personService.GetPersonAsync(id, ct);
        return person is null ? NotFound() : Ok(person);
    }

    /// <summary>Create a new person.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PersonDto), 201)]
    public async Task<ActionResult<PersonDto>> CreatePerson(
        [FromBody] CreatePersonRequest request, CancellationToken ct = default)
    {
        var person = await _personService.CreatePersonAsync(request, ct);
        return CreatedAtAction(nameof(GetPerson), new { id = person.Id }, person);
    }

    /// <summary>Update an existing person.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PersonDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<PersonDto>> UpdatePerson(
        Guid id, [FromBody] UpdatePersonRequest request, CancellationToken ct = default)
    {
        var person = await _personService.UpdatePersonAsync(id, request, ct);
        return person is null ? NotFound() : Ok(person);
    }

    /// <summary>Delete a person.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePerson(Guid id, CancellationToken ct = default)
    {
        return await _personService.DeletePersonAsync(id, ct) ? NoContent() : NotFound();
    }

    /// <summary>Upload a photo for a person.</summary>
    [HttpPost("{id:guid}/photos")]
    [ProducesResponseType(typeof(PersonPhotoDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<PersonPhotoDto>> UploadPhoto(
        Guid id, IFormFile photo, [FromForm] bool isPrimary = false, CancellationToken ct = default)
    {
        var photoDto = await _personService.AddPhotoAsync(id, photo, isPrimary, ct);
        return CreatedAtAction(nameof(GetPhotos), new { id }, photoDto);
    }

    /// <summary>Get all photos for a person.</summary>
    [HttpGet("{id:guid}/photos")]
    [ProducesResponseType(typeof(List<PersonPhotoDto>), 200)]
    public async Task<ActionResult<List<PersonPhotoDto>>> GetPhotos(Guid id, CancellationToken ct = default)
    {
        return Ok(await _personService.GetPhotosAsync(id, ct));
    }

    /// <summary>Delete a specific photo.</summary>
    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId, CancellationToken ct = default)
    {
        return await _personService.DeletePhotoAsync(id, photoId, ct) ? NoContent() : NotFound();
    }
}
