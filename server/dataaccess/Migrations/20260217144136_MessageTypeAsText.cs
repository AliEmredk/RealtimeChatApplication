using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dataaccess.Migrations
{
    /// <inheritdoc />
    public partial class MessageTypeAsText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Drop constraints that reference "type" (IF EXISTS)
            migrationBuilder.Sql("""
                                 ALTER TABLE messages DROP CONSTRAINT IF EXISTS dm_requires_recipient;
                                 """);

            migrationBuilder.Sql("""
                                 ALTER TABLE messages DROP CONSTRAINT IF EXISTS messages_type_valid;
                                 """);

            // 2) Convert enum -> text
            migrationBuilder.Sql("""
                                 ALTER TABLE messages
                                     ALTER COLUMN type TYPE text
                                     USING type::text;
                                 """);

            // 3) Drop enum type (optional)
            migrationBuilder.Sql("""
                                 DROP TYPE IF EXISTS public.message_type;
                                 """);

            // 4) Re-add constraints for TEXT column
            migrationBuilder.Sql("""
                                 ALTER TABLE messages
                                     ADD CONSTRAINT messages_type_valid CHECK (type IN ('public', 'dm'));
                                 """);

            migrationBuilder.Sql("""
                                 ALTER TABLE messages
                                     ADD CONSTRAINT dm_requires_recipient CHECK
                                     ((type = 'dm' AND recipient_user_id IS NOT NULL) OR
                                      (type = 'public' AND recipient_user_id IS NULL));
                                 """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                                 ALTER TABLE messages DROP CONSTRAINT IF EXISTS dm_requires_recipient;
                                 """);

            migrationBuilder.Sql("""
                                 ALTER TABLE messages DROP CONSTRAINT IF EXISTS messages_type_valid;
                                 """);

            migrationBuilder.Sql("""
                                 CREATE TYPE public.message_type AS ENUM ('public', 'dm');

                                 ALTER TABLE messages
                                     ALTER COLUMN type TYPE public.message_type
                                     USING type::public.message_type;

                                 ALTER TABLE messages
                                     ADD CONSTRAINT dm_requires_recipient CHECK
                                     ((type = 'dm' AND recipient_user_id IS NOT NULL) OR
                                      (type = 'public' AND recipient_user_id IS NULL));
                                 """);
        }


    }
}
