using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonIdentificationSystem.API.Migrations
{
    /// <inheritdoc />
    public partial class initialsetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_emails = table.Column<string[]>(type: "text[]", nullable: false),
                    minimum_confidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: false),
                    notify_on_risk_levels = table.Column<string[]>(type: "text[]", nullable: false),
                    rate_limit_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    smtp_host = table.Column<string>(type: "text", nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: true),
                    smtp_use_tls = table.Column<bool>(type: "boolean", nullable: true),
                    from_email = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    risk_level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Medium"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    date_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rtsp_streams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camera_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    camera_location = table.Column<string>(type: "text", nullable: true),
                    rtsp_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    frame_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Unknown"),
                    last_checked = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    date_added = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rtsp_streams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    quality_score = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    upload_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    original_filename = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_person_photos_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "detections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    detection_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    frame_image_url = table.Column<string>(type: "text", nullable: true),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verification_status = table.Column<string>(type: "text", nullable: true),
                    verified_by = table.Column<string>(type: "text", nullable: true),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verification_notes = table.Column<string>(type: "text", nullable: true),
                    email_sent = table.Column<bool>(type: "boolean", nullable: false),
                    raw_match_data = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_detections", x => x.id);
                    table.ForeignKey(
                        name: "fk_detections_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_detections_rtsp_streams_stream_id",
                        column: x => x.stream_id,
                        principalTable: "rtsp_streams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notification_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    detection_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sent_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    message_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_notification_logs_detections_detection_id",
                        column: x => x.detection_id,
                        principalTable: "detections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_detections_person_id",
                table: "detections",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_detections_stream_id",
                table: "detections",
                column: "stream_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_logs_detection_id",
                table: "notification_logs",
                column: "detection_id");

            migrationBuilder.CreateIndex(
                name: "ix_person_photos_person_id",
                table: "person_photos",
                column: "person_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_logs");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "person_photos");

            migrationBuilder.DropTable(
                name: "detections");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.DropTable(
                name: "rtsp_streams");
        }
    }
}
