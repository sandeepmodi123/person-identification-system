using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonIdentificationSystem.API.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonFaceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_detections_rtsp_streams_stream_id",
                table: "detections");

            migrationBuilder.AddColumn<string>(
                name: "person_face_id",
                table: "persons",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_persons_person_face_id",
                table: "persons",
                column: "person_face_id",
                unique: true,
                filter: "person_face_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_detections_rtsp_streams_stream_id",
                table: "detections",
                column: "stream_id",
                principalTable: "rtsp_streams",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_detections_rtsp_streams_stream_id",
                table: "detections");

            migrationBuilder.DropIndex(
                name: "ix_persons_person_face_id",
                table: "persons");

            migrationBuilder.DropColumn(
                name: "person_face_id",
                table: "persons");

            migrationBuilder.AddForeignKey(
                name: "fk_detections_rtsp_streams_stream_id",
                table: "detections",
                column: "stream_id",
                principalTable: "rtsp_streams",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
