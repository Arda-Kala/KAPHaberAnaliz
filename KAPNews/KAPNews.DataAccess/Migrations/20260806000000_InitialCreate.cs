using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KAPNews.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Haberler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HisseKodu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SirketAdi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Baslik = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Icerik = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YayinlanmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DuyguDurumu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EtkiVadesi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AiYorumu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SektorAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IndikatorSeti = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisclosureIndex = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    KapLinki = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EklerJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BildirimSinifi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Beklemede"),
                    KayitTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Haberler", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Haberler_DisclosureIndex",
                table: "Haberler",
                column: "DisclosureIndex");

            migrationBuilder.CreateIndex(
                name: "IX_Haberler_HisseKodu",
                table: "Haberler",
                column: "HisseKodu");

            migrationBuilder.CreateTable(
                name: "Kullanicilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SifreHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SonGirisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kullanicilar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Kullanicilar_KullaniciAdi",
                table: "Kullanicilar",
                column: "KullaniciAdi",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Kullanicilar");

            migrationBuilder.DropTable(
                name: "Haberler");
        }
    }
}
