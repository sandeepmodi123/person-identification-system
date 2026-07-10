using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonIdentificationSystem.API.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260709172000_AddCameraMobileNumberToRtspStreams")]
    public partial class AddCameraMobileNumberToRtspStreams : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "camera_mobile_number",
                table: "rtsp_streams",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "camera_mobile_number",
                table: "rtsp_streams");
        }
    }
}
