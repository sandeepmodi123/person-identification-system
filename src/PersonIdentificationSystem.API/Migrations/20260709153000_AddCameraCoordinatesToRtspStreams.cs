using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace PersonIdentificationSystem.API.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260709153000_AddCameraCoordinatesToRtspStreams")]
    public partial class AddCameraCoordinatesToRtspStreams : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "camera_latitude",
                table: "rtsp_streams",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "camera_longitude",
                table: "rtsp_streams",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_rtsp_streams_camera_latitude_range",
                table: "rtsp_streams",
                sql: "camera_latitude IS NULL OR (camera_latitude >= -90 AND camera_latitude <= 90)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_rtsp_streams_camera_longitude_range",
                table: "rtsp_streams",
                sql: "camera_longitude IS NULL OR (camera_longitude >= -180 AND camera_longitude <= 180)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_rtsp_streams_camera_latitude_range",
                table: "rtsp_streams");

            migrationBuilder.DropCheckConstraint(
                name: "ck_rtsp_streams_camera_longitude_range",
                table: "rtsp_streams");

            migrationBuilder.DropColumn(
                name: "camera_latitude",
                table: "rtsp_streams");

            migrationBuilder.DropColumn(
                name: "camera_longitude",
                table: "rtsp_streams");
        }
    }
}
