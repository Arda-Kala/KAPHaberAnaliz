using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KAPNews.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EtkiSkoruVeGunlukPiyasaSkoru : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 🌟 v2.3.0: Haber.EtkiSkoru — Gemini'nin ürettiği 0-100 arası
            // sayısal etki gücü. SPK Bülten mantığındaki gibi "Haber Genel
            // Skoru" ve "Piyasa Skoru" artık bu alandan hesaplanır.
            migrationBuilder.AddColumn<int>(
                name: "EtkiSkoru",
                table: "Haberler",
                type: "int",
                nullable: true);

            // 🌟 v2.3.0: Her takvim gününe ait, gün kapandıktan sonra (00:00'da)
            // hesaplanıp "dondurulan" Piyasa Skoru kayıtları.
            migrationBuilder.CreateTable(
                name: "GunlukPiyasaSkorlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NetSkor = table.Column<double>(type: "float", nullable: false),
                    Yon = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ToplamHaberSayisi = table.Column<int>(type: "int", nullable: false),
                    OlumluSayisi = table.Column<int>(type: "int", nullable: false),
                    OlumsuzSayisi = table.Column<int>(type: "int", nullable: false),
                    NotrSayisi = table.Column<int>(type: "int", nullable: false),
                    HesaplanmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GunlukPiyasaSkorlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GunlukPiyasaSkorlari_Tarih",
                table: "GunlukPiyasaSkorlari",
                column: "Tarih",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GunlukPiyasaSkorlari");

            migrationBuilder.DropColumn(
                name: "EtkiSkoru",
                table: "Haberler");
        }
    }
}
