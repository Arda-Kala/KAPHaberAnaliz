// ============================================================
// KAP Haberleri Analiz Paneli - app.js (v2.0.0)
// SPK Bülten Analiz Paneli ile aynı mimari desende yazılmıştır.
// ============================================================

// 🌟 DÜZELTME: Eskiden burada sabit "http://localhost:5120/api/Haberler" vardı —
// bu, projeyi başka bir portta/sunucuda çalıştırdığınızda (örn. Docker, IIS,
// production) API'nin asla bulunamamasına yol açardı. Göreceli path kullanarak
// dashboard'un HANGİ origin'den servis edildiyse API'yi de otomatik olarak o
// origin üzerinden çağırmasını sağlıyoruz.
const API_URL = "/api/Haberler";

// --- Global durum değişkenleri ---
let mevcutRol = "kullanici";
let tumHaberler = [];
let ekrandakiHaberler = [];
let mevcutSayfa = 1;
let sayfaBasinaKayit = 25; // Varsayılan: son 25 haber (SPK panelindeki gibi)
// Şu anda açık (detayı görünür) olan haber kartlarının id'lerini tutan küme.
// "Tümünü Aç/Kapat" ile çoklu, tek tek tıklamayla tekli çalışabilmesi için
// tekil bir id yerine her zaman bir Set kullanılır.
let acikDetayIdSeti = new Set();

// Sıralama
let aktifSiralamaKolonu = "tarih";
let siralamaYonu = "azalan"; // azalan = en yeni üstte

// Etki kategorileri — renklendirme ve sıralama için
const etkiRenkHaritasi = {
    "Yüksek Olumlu": "badge-positive", "Olumlu": "badge-positive",
    "Nötr": "badge-neutral", "Etkisiz": "badge-neutral",
    "Olumsuz": "badge-negative", "Yüksek Olumsuz": "badge-negative"
};

// ============================================================
// 🌟 v2.3.0 — HABER GENEL SKORU
// Artık sabit tablo yerine backend'den gelen gerçek EtkiSkoru alanından
// hesaplanıyor. Yön işareti DuyguDurumu'ndan, büyüklük EtkiSkoru'ndan gelir.
// EtkiSkoru yoksa (eski kayıtlar) geriye dönük uyum için sabit tablo kullanılır.
// ============================================================
function haberSkoruHesapla(dDurum, etkiSkoru) {
    // Backend'den gerçek sayısal skor geldiyse onu kullan (v2.3.0+)
    if (typeof etkiSkoru === "number" && etkiSkoru > 0) {
        if (["Olumlu", "Yüksek Olumlu"].includes(dDurum)) return etkiSkoru;
        if (["Olumsuz", "Yüksek Olumsuz"].includes(dDurum)) return -etkiSkoru;
        return 0;
    }
    // Geriye dönük uyum: eski kayıtlar için sabit tablo
    const eskiTablo = {
        "Yüksek Olumlu": 33, "Olumlu": 22,
        "Nötr": 0, "Etkisiz": 0,
        "Olumsuz": -22, "Yüksek Olumsuz": -33
    };
    if (!(dDurum in eskiTablo)) return null;
    return eskiTablo[dDurum];
}

let grafikInstance = null;

// ============================================================
// SAYFA YÜKLENINCE
// ============================================================
document.addEventListener("DOMContentLoaded", () => {
    if (localStorage.getItem("tema") === "acik") {
        document.documentElement.classList.remove("dark");
        document.getElementById("btnTema").innerHTML = '<i class="fa-solid fa-sun"></i>';
    }

    adminOturumKontrolEt();
    varsayilanTarihleriAyarla();
    verileriBackenddenCek();
    piyasaSkoruBackenddenCek();
    setInterval(verileriBackenddenCek, 15000);
    setInterval(piyasaSkoruBackenddenCek, 300000); // Piyasa skoru 5dk'da bir yenilenir
});

function adminOturumKontrolEt() {
    const token = localStorage.getItem("token");
    const rozet = document.getElementById("rozetKullanici");
    const btnCikis = document.getElementById("btnCikis");
    const btnAdmin = document.getElementById("btnAdmin");
    const btnAdminPanel = document.getElementById("btnAdminPanel");

    if (token) {
        mevcutRol = "admin";
        if (rozet) rozet.classList.remove("js-gizli");
        if (btnCikis) btnCikis.classList.remove("js-gizli");
        if (btnAdmin) btnAdmin.classList.add("js-gizli");
        if (btnAdminPanel) btnAdminPanel.classList.remove("js-gizli");
    } else {
        mevcutRol = "kullanici";
        if (rozet) rozet.classList.add("js-gizli");
        if (btnCikis) btnCikis.classList.add("js-gizli");
        if (btnAdmin) btnAdmin.classList.remove("js-gizli");
        if (btnAdminPanel) btnAdminPanel.classList.add("js-gizli");
    }
}

function yetkiliBasliklar() {
    const token = localStorage.getItem("token");
    return token ? { "Authorization": `Bearer ${token}` } : {};
}

// ============================================================
// 🌟 GENEL ÖZET — SPK panelindeki gibi: KPI'lar (Toplam/Olumlu/Nötr/
// Olumsuz/Piyasa Skoru) "Genel Özet" butonuna basınca yana doğru açılır.
// Artık admin'e özel değil, giriş yapmadan da görülebilir.
// ============================================================
function toggleGenelOzet() {
    const alan = document.getElementById("genelOzetContent");
    const ikon = document.getElementById("genelOzetChevron");
    if (!alan) return;

    const acik = alan.classList.contains("flex");
    if (acik) {
        alan.classList.remove("flex");
        alan.classList.add("hidden");
        if (ikon) ikon.classList.add("-rotate-90");
    } else {
        alan.classList.remove("hidden");
        alan.classList.add("flex");
        if (ikon) ikon.classList.remove("-rotate-90");
    }
}

// ============================================================
// 🌟 SPK panelindeki gibi: header'daki "Aksiyonlar" çubuğu tıklanınca
// açılıp kapanan (Tara / Grafik / Excel / Temizle) buton grubu.
// ============================================================
function toggleAksiyonlar() {
    const alan = document.getElementById("aksiyonlarContent");
    const ikon = document.getElementById("aksiyonlarChevron");
    if (!alan) return;

    const acik = alan.classList.contains("flex");
    if (acik) {
        alan.classList.remove("flex");
        alan.classList.add("hidden");
        if (ikon) ikon.classList.add("-rotate-90");
    } else {
        alan.classList.remove("hidden");
        alan.classList.add("flex");
        if (ikon) ikon.classList.remove("-rotate-90");
    }
}

// ============================================================
// 🌟 FİLTRE MODALI — SPK Bülten panelindeki gibi, detaylı ve pop-up
// (ekranın ortasında açılan modal) şeklinde. Kelime arama, tarih aralığı,
// sıralama, yön (duygu durumu) çoklu seçim, minimum etki skoru (slider) ve
// sektör checkbox grid'i içerir. Değişiklikler yalnızca "Uygula" butonuna
// basılınca gerçek listeye yansır; modal içindeki taslak değerler ayrı
// tutulur ki "Vazgeç" ile iptal edilebilsin.
// ============================================================

// Uygulanan (aktif) filtre durumu — listeleme bu değerlere göre yapılır.
// Tarihler artık HTML input'tan değil doğrudan bu state'ten okunur.
let aktifFiltre = {
    yonler: new Set(["Olumlu", "Notr", "Olumsuz"]),
    minEtkiSkoru: 0,
    secilenSektorler: new Set(),
    tarihBaslangic: "",  // varsayilanTarihleriAyarla() doldurur
    tarihBitis: ""
};

function filtrePaneliAc() {
    const modal = document.getElementById("modalFiltre");
    if (!modal) return;

    // Modal içindeki alanları mevcut aktif filtreyle senkronize et
    document.getElementById("txtFiltreModal").value = document.getElementById("txtFiltre").value;
    document.getElementById("tarihBaslangicModal").value = aktifFiltre.tarihBaslangic;
    document.getElementById("tarihBitisModal").value = aktifFiltre.tarihBitis;
    document.getElementById("siralamaSecimiModal").value = `${aktifSiralamaKolonu}-${siralamaYonu}`;
    document.getElementById("etkiSkoruSlider").value = aktifFiltre.minEtkiSkoru;
    document.getElementById("etkiSkoruDeger").innerText = `%${aktifFiltre.minEtkiSkoru}`;

    document.querySelectorAll(".yon-filtre-checkbox").forEach(cb => {
        cb.checked = aktifFiltre.yonler.has(cb.value);
    });

    sektorCheckboxlariniOlustur();
    modalSayfaBoyutuGorseliniGuncelle();
    modal.classList.remove("js-gizli");
}

function filtrePaneliKapat() {
    document.getElementById("modalFiltre")?.classList.add("js-gizli");
}

// Modal içindeki tüm seçimleri okuyup aktif filtreye yazar ve listeyi yeniler.
function filtreleriUygula() {
    document.getElementById("txtFiltre").value = document.getElementById("txtFiltreModal").value;

    // Tarihler artık aktifFiltre state'ine yazılır (HTML'de gizli input yok)
    aktifFiltre.tarihBaslangic = document.getElementById("tarihBaslangicModal").value;
    aktifFiltre.tarihBitis = document.getElementById("tarihBitisModal").value;

    // "tarih-azalan" → kolon="tarih", yon="azalan"
    const parcalar = document.getElementById("siralamaSecimiModal").value.split("-");
    aktifSiralamaKolonu = parcalar[0];             // "tarih" | "etki" | "vade"
    siralamaYonu = parcalar[parcalar.length - 1];  // "azalan" | "artan"

    aktifFiltre.yonler = new Set(
        [...document.querySelectorAll(".yon-filtre-checkbox:checked")].map(cb => cb.value)
    );
    aktifFiltre.minEtkiSkoru = parseInt(document.getElementById("etkiSkoruSlider").value, 10) || 0;
    aktifFiltre.secilenSektorler = new Set(
        [...document.querySelectorAll(".sektor-filtre-checkbox:checked")].map(cb => cb.value)
    );

    mevcutSayfa = 1;
    filtrePaneliKapat();
    ekranaHaberleriBas();
    filtreAktifRozetiGuncelle();
}

// "Etki Skoru" slider'ı canlı olarak yüzde değerini günceller.
function etkiSkoruSliderGuncelle(deger) {
    document.getElementById("etkiSkoruDeger").innerText = `%${deger}`;
}

// Sektör checkbox grid'ini tumHaberler'den dinamik olarak oluşturur.
function sektorCheckboxlariniOlustur() {
    const konteyner = document.getElementById("sektorFiltreGrubu");
    if (!konteyner) return;

    const sektorler = [...new Set(tumHaberler.map(h => h.sektorAdi || h.SektorAdi).filter(Boolean))].sort();

    if (sektorler.length === 0) {
        konteyner.innerHTML = `<p class="text-xs text-slate-400 col-span-full">Henüz sektör bilgisi bulunmuyor.</p>`;
        return;
    }

    konteyner.innerHTML = sektorler.map(s => `
        <label class="flex items-center gap-1.5 text-xs cursor-pointer">
            <input type="checkbox" value="${s}" class="sektor-filtre-checkbox accent-brand dark:accent-accent"
                   ${aktifFiltre.secilenSektorler.size === 0 || aktifFiltre.secilenSektorler.has(s) ? "checked" : ""}>
            <span class="truncate">${s}</span>
        </label>`).join("");
}

function sektorHepsiniSec(secilsinMi) {
    document.querySelectorAll(".sektor-filtre-checkbox").forEach(cb => cb.checked = secilsinMi);
}

// Modal içindeki 25/50/100 sayfa boyutu butonları.
function modalSayfaBoyutuSec(deger) {
    kayitSayisiDegistir(deger);
    modalSayfaBoyutuGorseliniGuncelle();
}

function modalSayfaBoyutuGorseliniGuncelle() {
    [25, 50, 100].forEach(boyut => {
        const btn = document.getElementById(`modalSayfa${boyut}`);
        if (btn) btn.classList.toggle("aktif", boyut === sayfaBasinaKayit);
    });
}

// Tarih aralığı, yön, min etki skoru veya sektör filtresi aktifse "Filtrele"
// butonunun yanında küçük bir nokta gösterir.
function filtreAktifRozetiGuncelle() {
    const rozet = document.getElementById("filtreAktifRozeti");
    if (!rozet) return;

    const varsayilanTarihMi = aktifFiltre.tarihBaslangic === varsayilanBaslangicTarihi
        && aktifFiltre.tarihBitis === varsayilanBitisTarihi;
    const tumYonlerSecili = aktifFiltre.yonler.size === 3;
    const tumSektorlerSecili = aktifFiltre.secilenSektorler.size === 0;
    const aktif = !varsayilanTarihMi || !tumYonlerSecili || aktifFiltre.minEtkiSkoru > 0 || !tumSektorlerSecili;
    rozet.classList.toggle("js-gizli", !aktif);
}

// ============================================================
// TEMA
// ============================================================
function temaDegistir() {
    document.documentElement.classList.toggle("dark");
    const karanlik = document.documentElement.classList.contains("dark");
    localStorage.setItem("tema", karanlik ? "karanlik" : "acik");
    document.getElementById("btnTema").innerHTML = karanlik
        ? '<i class="fa-solid fa-moon"></i>' : '<i class="fa-solid fa-sun"></i>';
}

// ============================================================
// TARİH
// ============================================================
// Filtre panelindeki "aktif filtre" rozetinin karşılaştırma yapabilmesi için
// varsayılan (son 30 gün) tarih aralığını global olarak saklıyoruz.
let varsayilanBaslangicTarihi = "";
let varsayilanBitisTarihi = "";

function varsayilanTarihleriAyarla() {
    const bugun = new Date();
    const birAyOncesi = new Date();
    birAyOncesi.setDate(bugun.getDate() - 30);

    varsayilanBitisTarihi = bugun.toISOString().split('T')[0];
    varsayilanBaslangicTarihi = birAyOncesi.toISOString().split('T')[0];

    // Tarihler artık aktifFiltre state'inde tutuluyor (HTML input yok)
    aktifFiltre.tarihBaslangic = varsayilanBaslangicTarihi;
    aktifFiltre.tarihBitis = varsayilanBitisTarihi;
}

// ============================================================
// VERİ ÇEKME
// ============================================================
async function verileriBackenddenCek() {
    try {
        const yanit = await fetch(API_URL);
        if (!yanit.ok) throw new Error(`HTTP ${yanit.status}`);
        tumHaberler = await yanit.json();
        ekranaHaberleriBas();
    } catch (hata) {
        console.error("Haberler çekilemedi:", hata);
    }
}

// ============================================================
// 🌟 v2.0.0: YENİ HABERLERİ TARA BUTONU
// ============================================================
async function yeniHaberleriTara() {
    const token = localStorage.getItem("token");
    if (!token) {
        popupGoster("Bu işlem için admin girişi gereklidir.", "hata");
        adminGirisPencerisiniAc();
        return;
    }

    const btn = document.getElementById("btnYeniHaberleriTara");
    const icon = document.getElementById("iconTara");
    btn.disabled = true;
    icon.classList.add("animate-spin-slow");

    try {
        const yanit = await fetch(`${API_URL}/tetikle-kap`, {
            method: "POST",
            headers: { ...yetkiliBasliklar() }
        });

        if (!yanit.ok) {
            if (yanit.status === 401 || yanit.status === 403) {
                popupGoster("Oturumunuz sona ermiş, tekrar giriş yapın.", "hata");
                cikisYap();
                return;
            }
            throw new Error(`HTTP ${yanit.status}`);
        }

        const sonuc = await yanit.json();
        popupGoster(`Tarama tamamlandı: ${sonuc.eklenenHaberSayisi} yeni bildirim eklendi.`, "basarili");
        await verileriBackenddenCek();
    } catch (hata) {
        console.error(hata);
        popupGoster("Tarama sırasında bir hata oluştu.", "hata");
    } finally {
        btn.disabled = false;
        icon.classList.remove("animate-spin-slow");
    }
}

// ============================================================
// 🌟 v2.0.0: BİR HABERİ YENİDEN ANALİZ ET
// ============================================================
async function haberiYenidenAnalizEt(id, event) {
    if (event) event.stopPropagation(); // Kartın açılıp kapanmasını tetiklemesin

    const token = localStorage.getItem("token");
    if (!token) {
        popupGoster("Bu işlem için admin girişi gereklidir.", "hata");
        adminGirisPencerisiniAc();
        return;
    }

    const buton = document.getElementById(`btnYenidenAnalizle-${id}`);
    const orijinalIcerik = buton ? buton.innerHTML : "";
    if (buton) {
        buton.disabled = true;
        buton.innerHTML = '<i class="fa-solid fa-spinner animate-spin-slow"></i> Analiz ediliyor...';
    }

    try {
        const yanit = await fetch(`${API_URL}/yeniden-tara/${id}`, {
            method: "POST",
            headers: { ...yetkiliBasliklar() }
        });

        if (!yanit.ok) throw new Error(`HTTP ${yanit.status}`);

        popupGoster("Haber yeniden analiz edildi.", "basarili");
        await verileriBackenddenCek();
        acikDetayIdSeti.add(id); // Kartı açık tut, yeniden bas
        ekranaHaberleriBas();
    } catch (hata) {
        console.error(hata);
        popupGoster("Yeniden analiz sırasında hata oluştu.", "hata");
        if (buton) { buton.disabled = false; buton.innerHTML = orijinalIcerik; }
    }
}

// ============================================================
// 🌟 v2.0.0: ŞİRKET GEÇMİŞİ (şirket ismine tıklayınca)
// ============================================================
async function sirketGecmisiniGoster(hisseKodu, event) {
    if (event) event.stopPropagation();
    if (!hisseKodu) return;

    document.getElementById("gecmisBaslik").innerText = `${hisseKodu} — Geçmiş Bildirimler`;
    const liste = document.getElementById("gecmisListesi");
    liste.innerHTML = `<p class="text-sm text-slate-400 text-center py-6"><i class="fa-solid fa-spinner animate-spin-slow mr-2"></i>Yükleniyor...</p>`;
    document.getElementById("modalSirketGecmisi").classList.remove("js-gizli");

    try {
        const yanit = await fetch(`${API_URL}/sirket-gecmisi/${encodeURIComponent(hisseKodu)}`);
        if (!yanit.ok) throw new Error(`HTTP ${yanit.status}`);
        const gecmis = await yanit.json();

        if (gecmis.length === 0) {
            liste.innerHTML = `<p class="text-sm text-slate-400 text-center py-6">Bu şirkete ait geçmiş bildirim bulunamadı.</p>`;
            return;
        }

        liste.innerHTML = gecmis.map(h => {
            const dDurum = h.duyguDurumu || h.DuyguDurumu || "";
            const renk = etkiRenkHaritasi[dDurum] || "badge-neutral";
            const tarih = formatTarih(h.yayinlanmaTarihi || h.YayinlanmaTarihi);
            const baslik = h.baslik || h.Baslik || "";
            const kapLinki = h.kapLinki || h.KapLinki || "";
            return `
            <div class="dashboard-card rounded-xl p-3">
                <div class="flex items-center justify-between gap-2">
                    <div class="flex-1 min-w-0">
                        <p class="text-xs text-slate-400">${tarih}</p>
                        <p class="text-sm font-medium truncate">${baslik}</p>
                    </div>
                    <span class="px-2 py-1 rounded-full text-xs font-semibold whitespace-nowrap ${renk}">${dDurum || "Bekliyor"}</span>
                    ${kapLinki ? `<a href="${kapLinki}" target="_blank" rel="noopener" class="text-brand dark:text-accent hover:underline text-xs whitespace-nowrap"><i class="fa-solid fa-arrow-up-right-from-square"></i></a>` : ""}
                </div>
            </div>`;
        }).join("");
    } catch (hata) {
        console.error(hata);
        liste.innerHTML = `<p class="text-sm text-rose-500 text-center py-6">Geçmiş yüklenirken hata oluştu.</p>`;
    }
}

// ============================================================
// 🌟 v2.2.0 — HİSSE FİYAT GRAFİĞİ (Yahoo Finance)
// Eski projedeki "KAP haberine tıklayınca gelen Yahoo Finance hisse grafiği"
// özelliğinin karşılığı. Hisse koduna tıklanınca açılır; backend'deki
// api/Finance/chart/{symbol} uç noktasını kullanır. BIST hisseleri Yahoo
// Finance'te ".IS" uzantısıyla işlem görür (örn. ASELS -> ASELS.IS).
// ============================================================
let hisseGrafikChart = null; // Chart.js örneği — yeniden çizerken eskisini yok etmek için
let hisseGrafikAktifSembol = "";
let hisseGrafikAktifGun = 30;

function hisseGrafigiGoster(hisseKodu, sirketAdi, event) {
    if (event) event.stopPropagation();
    if (!hisseKodu) {
        popupGoster("Bu haber için hisse kodu bulunamadı.", "hata");
        return;
    }

    hisseGrafikAktifSembol = hisseKodu;
    document.getElementById("hisseGrafikBaslik").innerText = `${hisseKodu} — ${sirketAdi || "Hisse Fiyat Grafiği"}`;
    document.getElementById("modalHisseGrafik").classList.remove("js-gizli");

    hisseGrafikDonemDegistir(30); // varsayılan: 1 ay
}

function hisseGrafikDonemDegistir(gun) {
    hisseGrafikAktifGun = gun;

    // Aktif dönem butonunu vurgula
    document.querySelectorAll(".hisse-grafik-donem-btn").forEach(btn => {
        const aktif = parseInt(btn.dataset.gun, 10) === gun;
        btn.classList.toggle("bg-brand", aktif);
        btn.classList.toggle("text-white", aktif);
        btn.classList.toggle("border-brand", aktif);
    });

    hisseGrafikVerisiCekVeCiz(hisseGrafikAktifSembol, gun);
}

async function hisseGrafikVerisiCekVeCiz(hisseKodu, gun) {
    const yukleniyor = document.getElementById("hisseGrafikYukleniyor");
    const hataAlani = document.getElementById("hisseGrafikHata");
    const canvas = document.getElementById("hisseGrafikCanvas");

    yukleniyor.classList.remove("js-gizli");
    hataAlani.classList.add("js-gizli");
    canvas.classList.add("js-gizli");

    // BIST hisseleri Yahoo Finance'te ".IS" uzantısıyla işlem görür.
    const yahooSembol = hisseKodu.includes(".") ? hisseKodu : `${hisseKodu}.IS`;
    const interval = gun <= 30 ? "1d" : gun <= 90 ? "1d" : "1wk";

    try {
        const yanit = await fetch(`/api/Finance/chart/${encodeURIComponent(yahooSembol)}?days=${gun}&interval=${interval}`);
        if (!yanit.ok) throw new Error(`HTTP ${yanit.status}`);

        const veri = await yanit.json();
        const sonuc = veri?.chart?.result?.[0];
        if (!sonuc || !sonuc.timestamp || sonuc.timestamp.length === 0) {
            throw new Error("Boş veri seti");
        }

        const zamanlar = sonuc.timestamp.map(t => new Date(t * 1000).toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit" }));
        const kapanisFiyatlari = sonuc.indicators?.quote?.[0]?.close || [];

        yukleniyor.classList.add("js-gizli");
        canvas.classList.remove("js-gizli");

        if (hisseGrafikChart) hisseGrafikChart.destroy();

        const koyuTema = document.documentElement.classList.contains("dark");
        const cizgiRengi = "#F2994A";
        const izgaraRengi = koyuTema ? "rgba(148,163,184,0.15)" : "rgba(27,59,111,0.1)";
        const metinRengi = koyuTema ? "#cbd5e1" : "#334155";

        hisseGrafikChart = new Chart(canvas.getContext("2d"), {
            type: "line",
            data: {
                labels: zamanlar,
                datasets: [{
                    label: `${hisseKodu} Kapanış Fiyatı (₺)`,
                    data: kapanisFiyatlari,
                    borderColor: cizgiRengi,
                    backgroundColor: "rgba(242,153,74,0.12)",
                    fill: true,
                    tension: 0.25,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { mode: "index", intersect: false }
                },
                scales: {
                    x: { grid: { color: izgaraRengi }, ticks: { color: metinRengi, maxTicksLimit: 8 } },
                    y: { grid: { color: izgaraRengi }, ticks: { color: metinRengi } }
                }
            }
        });
    } catch (hata) {
        console.error("Hisse grafiği hatası:", hata);
        yukleniyor.classList.add("js-gizli");
        canvas.classList.add("js-gizli");
        hataAlani.classList.remove("js-gizli");
    }
}

// ============================================================
// 🌟 v2.2.0 — PDF EKİNİ ANALİZ EDİP YORUMU DETAYLANDIR (admin paneli)
// Haber kartındaki "PDF Ekini Analiz Et" butonuna basılınca çağrılır.
// Backend, bildirim eki PDF'ini KAP'tan indirip Gemini'ye metinle birlikte
// gönderir ve daha detaylı bir aiYorumu döner.
// ============================================================
async function pdfEkiniAnalizEt(id, event) {
    if (event) event.stopPropagation();

    const token = localStorage.getItem("token");
    if (!token) {
        popupGoster("Bu işlem için admin girişi gereklidir.", "hata");
        adminGirisPencerisiniAc();
        return;
    }

    const buton = document.getElementById(`btnPdfAnalizEt-${id}`);
    const orijinalIcerik = buton ? buton.innerHTML : "";
    if (buton) {
        buton.disabled = true;
        buton.innerHTML = '<i class="fa-solid fa-spinner animate-spin-slow"></i> PDF analiz ediliyor...';
    }

    try {
        const yanit = await fetch(`${API_URL}/pdf-analiz-detaylandir/${id}`, {
            method: "POST",
            headers: { ...yetkiliBasliklar() }
        });

        if (!yanit.ok) {
            const hataMetni = await yanit.text().catch(() => "");
            throw new Error(hataMetni || `HTTP ${yanit.status}`);
        }

        popupGoster("PDF eki analiz edildi, yorum detaylandırıldı.", "basarili");
        await verileriBackenddenCek();
        acikDetayIdSeti.add(id); // Kartı açık tut, yeniden bas
        ekranaHaberleriBas();
    } catch (hata) {
        console.error(hata);
        popupGoster("PDF eki analiz edilirken hata oluştu.", "hata");
        if (buton) { buton.disabled = false; buton.innerHTML = orijinalIcerik; }
    }
}

// ============================================================
// SIRALAMA
// ============================================================
// Sıralama artık filtre modalındaki tek bir <select> ile kontrol ediliyor
// (bkz. filtreleriUygula) — ayrı kolon/yön butonlarına gerek kalmadı.

// ============================================================
// 🌟 TÜMÜNÜ AÇ / KAPAT — SPK panelindeki gibi, ekranda listelenen tüm
// bildirim kartlarının detaylarını tek tuşla açar/kapatır.
// ============================================================
let tumKartlarAcikMi = false;

function tumunuAcKapat() {
    tumKartlarAcikMi = !tumKartlarAcikMi;

    if (tumKartlarAcikMi) {
        // Şu an ekranda görünen (filtrelenmiş/sayfalanmış) tüm haberlerin
        // id'lerini "açık" olarak işaretle.
        acikDetayIdSeti = new Set(ekrandakiHaberler.map(h => h.id || h.Id));
    } else {
        acikDetayIdSeti.clear();
    }

    ekranaHaberleriBas();
    tumunuAcKapatButonunuGuncelle();
}

function tumunuAcKapatButonunuGuncelle() {
    const metin = document.getElementById("tumunuAcKapatMetin");
    const ikon = document.getElementById("tumunuAcKapatIkon");
    if (!metin || !ikon) return;

    metin.innerText = tumKartlarAcikMi ? "Tümünü Kapat" : "Tümünü Aç";
    ikon.classList.toggle("rotate-180", tumKartlarAcikMi);
}

function kayitSayisiDegistir(deger) {
    sayfaBasinaKayit = parseInt(deger, 10) || 25;
    mevcutSayfa = 1;
    ekranaHaberleriBas();
}

function sayfayaGit(no) { mevcutSayfa = no; ekranaHaberleriBas(); window.scrollTo({ top: 0, behavior: "smooth" }); }

function filtreleriTemizle() {
    document.getElementById("txtFiltre").value = "";
    const modal = document.getElementById("txtFiltreModal");
    if (modal) modal.value = "";
    varsayilanTarihleriAyarla(); // aktifFiltre.tarihBaslangic/Bitis'i de sıfırlar

    aktifSiralamaKolonu = "tarih";
    siralamaYonu = "azalan";
    aktifFiltre.yonler = new Set(["Olumlu", "Notr", "Olumsuz"]);
    aktifFiltre.minEtkiSkoru = 0;
    aktifFiltre.secilenSektorler = new Set();

    const siralamaSecici = document.getElementById("siralamaSecimiModal");
    if (siralamaSecici) siralamaSecici.value = "tarih-azalan";
    const slider = document.getElementById("etkiSkoruSlider");
    if (slider) { slider.value = 0; }
    const skorEtiketi = document.getElementById("etkiSkoruDeger");
    if (skorEtiketi) skorEtiketi.innerText = "%0";
    document.querySelectorAll(".yon-filtre-checkbox").forEach(cb => cb.checked = true);
    document.querySelectorAll(".sektor-filtre-checkbox").forEach(cb => cb.checked = true);

    mevcutSayfa = 1;
    ekranaHaberleriBas();
    filtreAktifRozetiGuncelle();
}

// ============================================================
// ANA RENDER FONKSİYONU — KART TABANLI (SPK stiline uygun)
// ============================================================
function ekranaHaberleriBas() {
    const arama = (document.getElementById("txtFiltre")?.value || "").toLowerCase().trim();
    const baslangic = aktifFiltre.tarihBaslangic;
    const bitis = aktifFiltre.tarihBitis;

    const konteyner = document.getElementById("haberListesi");
    if (!konteyner) return;

    // "Olumlu/Yüksek Olumlu", "Nötr/Etkisiz", "Olumsuz/Yüksek Olumsuz" grup eşlemesi —
    // filtre modalındaki 3 checkbox'ın karşılığı.
    const yonGrubu = (duyguDurumu) => {
        if (["Olumlu", "Yüksek Olumlu"].includes(duyguDurumu)) return "Olumlu";
        if (["Olumsuz", "Yüksek Olumsuz"].includes(duyguDurumu)) return "Olumsuz";
        if (["Nötr", "Etkisiz"].includes(duyguDurumu)) return "Notr";
        return null; // analiz edilmemiş
    };

    // ------ FİLTRELEME ------
    let liste = tumHaberler.filter(h => {
        const hisse = (h.hisseKodu || h.HisseKodu || "").toLowerCase();
        const sirket = (h.sirketAdi || h.SirketAdi || "").toLowerCase();
        const baslik = (h.baslik || h.Baslik || "").toLowerCase();
        const sek = h.sektorAdi || h.SektorAdi || "";
        const dDurum = h.duyguDurumu || h.DuyguDurumu || "";

        const metinUygun = !arama || hisse.includes(arama) || sirket.includes(arama) || baslik.includes(arama);

        const sektorUygun = aktifFiltre.secilenSektorler.size === 0 || aktifFiltre.secilenSektorler.has(sek);

        // Analiz edilmemiş haberler (yön grubu null) her zaman gösterilir —
        // aksi halde henüz analiz sırasını bekleyen haberler listeden düşer.
        const grup = yonGrubu(dDurum);
        const yonUygun = grup === null || aktifFiltre.yonler.has(grup);

        const hEtkiSkoru = h.etkiSkoru ?? h.EtkiSkoru ?? null;
        const skor = haberSkoruHesapla(dDurum, hEtkiSkoru);
        const etkiSkoruUygun = aktifFiltre.minEtkiSkoru === 0 || (skor !== null && Math.abs(skor) >= aktifFiltre.minEtkiSkoru);

        let tarihUygun = true;
        const tStr = h.yayinlanmaTarihi || h.YayinlanmaTarihi || "";
        if (tStr) {
            const ht = new Date(tStr);
            const gun = new Date(ht.getFullYear(), ht.getMonth(), ht.getDate());
            if (baslangic && gun < new Date(baslangic)) tarihUygun = false;
            if (bitis && gun > new Date(bitis)) tarihUygun = false;
        }
        return metinUygun && sektorUygun && yonUygun && etkiSkoruUygun && tarihUygun;
    });

    // ------ SIRALAMA ------
    const yon = siralamaYonu === "azalan" ? -1 : 1;
    liste.sort((a, b) => {
        if (aktifSiralamaKolonu === "tarih") {
            return yon * (new Date(a.yayinlanmaTarihi || a.YayinlanmaTarihi) - new Date(b.yayinlanmaTarihi || b.YayinlanmaTarihi));
        }
        if (aktifSiralamaKolonu === "etki") {
            const siraKey = ["Yüksek Olumsuz", "Olumsuz", "Nötr", "Etkisiz", "Olumlu", "Yüksek Olumlu"];
            const iA = siraKey.indexOf(a.duyguDurumu || a.DuyguDurumu);
            const iB = siraKey.indexOf(b.duyguDurumu || b.DuyguDurumu);
            return yon * ((iA === -1 ? -2 : iA) - (iB === -1 ? -2 : iB));
        }
        if (aktifSiralamaKolonu === "vade") {
            const siraKey = ["Anlık", "Kısa Vadeli", "Orta Vadeli", "Uzun Vadeli"];
            const iA = siraKey.indexOf(a.etkiVadesi || a.EtkiVadesi);
            const iB = siraKey.indexOf(b.etkiVadesi || b.EtkiVadesi);
            return yon * ((iA === -1 ? -2 : iA) - (iB === -1 ? -2 : iB));
        }
        return 0;
    });

    ekrandakiHaberler = liste;
    document.getElementById("haberSayisi").innerText = liste.length;

    if (liste.length === 0) {
        konteyner.innerHTML = `<div class="text-center py-16 text-slate-400"><i class="fa-solid fa-inbox text-3xl mb-2"></i><p>Belirtilen kriterlerde bildirim bulunamadı.</p></div>`;
        document.getElementById("sayfalamaKonteyneri").innerHTML = "";
        return;
    }

    const toplamSayfa = Math.ceil(liste.length / sayfaBasinaKayit);
    if (mevcutSayfa > toplamSayfa) mevcutSayfa = toplamSayfa;
    const bas = (mevcutSayfa - 1) * sayfaBasinaKayit;
    const sayfa = liste.slice(bas, bas + sayfaBasinaKayit);

    konteyner.innerHTML = sayfa.map(h => haberKartiOlustur(h)).join("");

    sayfalamaButonlariniOlustur(toplamSayfa);
    kpiKartlariniGuncelle();
}

// ============================================================
// 🌟 HABER KARTI — SPK'daki bülten kartlarıyla aynı görsel dil.
// Genel özet her zaman görünür; detay (tam metin + KAP linki + ekler)
// açılır/kapanır bir bölümde, tıklanınca açılır.
// ============================================================
function haberKartiOlustur(h) {
    const kartAyarlari = kartAyarlariyukle();
    const hId = h.id || h.Id || 0;
    const hKodu = h.hisseKodu || h.HisseKodu || "";
    const sAdi = h.sirketAdi || h.SirketAdi || "";
    const hBaslik = h.baslik || h.Baslik || "";
    const hIcerik = h.icerik || h.Icerik || "";
    const dDurum = h.duyguDurumu || h.DuyguDurumu || "";
    const eVadesi = h.etkiVadesi || h.EtkiVadesi || "";
    const sekAdi = h.sektorAdi || h.SektorAdi || "";
    const aYorum = h.aiYorumu || h.AiYorumu || "";
    const tarih = formatTarih(h.yayinlanmaTarihi || h.YayinlanmaTarihi);
    const kapLinki = h.kapLinki || h.KapLinki || "";
    const indStr = h.indikatorSeti || h.IndikatorSeti || "";
    const eklerStr = h.eklerJson || h.EklerJson || "";

    const renk = etkiRenkHaritasi[dDurum] || "badge-neutral";
    const acik = acikDetayIdSeti.has(hId);
    const etkiSkoru = h.etkiSkoru ?? h.EtkiSkoru ?? null; // v2.3.0: gerçek AI skoru

    let indikatorHtml = "";
    if (indStr) {
        try {
            const indList = typeof indStr === "string" ? JSON.parse(indStr) : indStr;
            indikatorHtml = indList.map(i => `<span class="px-2 py-0.5 rounded-full bg-slate-200 dark:bg-slate-800 text-xs">${i}</span>`).join(" ");
        } catch { /* ihmal — bozuk JSON ise indikatör gösterilmez */ }
    }

    let eklerHtml = "";
    let ilkPdfUrl = "";
    try {
        const ekler = eklerStr ? (typeof eklerStr === "string" ? JSON.parse(eklerStr) : eklerStr) : [];
        if (ekler.length > 0) {
            ilkPdfUrl = ekler[0].url || ekler[0].Url || "";
            eklerHtml = `
            <div>
                <p class="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400 mb-2 flex items-center gap-1.5">
                    <i class="fa-solid fa-paperclip"></i>Bildirim Ekleri
                </p>
                <div class="flex flex-wrap gap-2">
                    ${ekler.map(ek => `
                        <a href="${ek.url || ek.Url}" target="_blank" rel="noopener" onclick="event.stopPropagation()"
                           class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-300 dark:border-slate-700 hover:border-accent text-xs transition"
                           title="PDF'i yeni sekmede aç">
                            <i class="fa-solid fa-file-pdf text-rose-500"></i>${ek.dosyaAdi || ek.DosyaAdi || "Ek Dosya"}
                        </a>`).join("")}
                </div>
            </div>`;
        }
    } catch { /* ihmal */ }

    // ------ HABER GENEL SKORU — v2.3.0: gerçek EtkiSkoru alanından ------
    const skor = haberSkoruHesapla(dDurum, etkiSkoru);
    let skorHtml = "";
    if (skor !== null && kartAyarlari.skorGoster) {
        const pozitif = skor > 0;
        const notr = skor === 0;
        const skorRenkSinifi = notr
            ? "bg-amber-500/10 border-amber-400/30 text-amber-500"
            : pozitif
                ? "bg-emerald-500/10 border-emerald-400/30 text-emerald-500"
                : "bg-rose-500/10 border-rose-400/30 text-rose-500";
        const skorIkon = notr ? "fa-minus" : pozitif ? "fa-arrow-trend-up" : "fa-arrow-trend-down";
        const skorEtiket = notr ? "Nötr" : pozitif ? "Pozitif" : "Negatif";
        skorHtml = `
            <div class="flex items-center gap-1.5 shrink-0 px-2 py-1.5 sm:px-3 sm:py-2 rounded-xl border ${skorRenkSinifi}">
                <i class="fa-solid ${skorIkon} text-sm"></i>
                <div class="leading-tight text-right">
                    <p class="text-[10px] font-bold uppercase tracking-wide opacity-80">Haber Genel Skoru</p>
                    <p class="text-xs sm:text-sm font-black">%${Math.abs(skor)} <span class="font-medium text-[10px] sm:text-[11px] opacity-80">(${skorEtiket})</span></p>
                </div>
            </div>`;
    }

    return `
    <div class="dashboard-card rounded-xl overflow-hidden">
        <div class="p-3 sm:p-4 cursor-pointer" onclick="detayAcKapat(${hId})">
            <div class="flex items-start justify-between gap-2">
                <div class="flex-1 min-w-0">
                    <!-- ÜST SATIR: Şirket / hisse kodu (tıklanınca hisse grafiği açılır) -->
                    <div class="flex items-center gap-2 flex-wrap mb-1.5">
                        <span class="font-bold text-brand dark:text-accent hover:underline cursor-pointer"
                              onclick="hisseGrafigiGoster('${hKodu}', '${sAdi.replace(/'/g, "\\'")}', event)" title="Hisse fiyat grafiğini gör">
                            <i class="fa-solid fa-chart-line text-[11px] mr-1 opacity-70"></i>${hKodu || "—"}
                        </span>
                        <span class="text-sm text-slate-500 dark:text-slate-400 truncate">${sAdi}</span>
                    </div>

                    <!-- BAŞLIK SATIRI -->
                    <div class="flex items-center gap-2.5 flex-wrap">
                        <p class="font-bold text-sm sm:text-base leading-snug">${hBaslik}</p>
                    </div>

                    <!-- YAYIN TARİHİ: SPK panelindeki gibi başlığın ALTINDA, "Yayın Tarihi:" etiketiyle -->
                    <p class="text-xs text-slate-400 mt-1"><i class="fa-regular fa-calendar mr-1"></i>Yayın Tarihi: ${tarih}</p>

                    <!-- 🌟 KART KAPALIYKEN DE YAPAY ZEKA YORUMU GÖRÜNSÜN (SPK panelindeki
                    gibi) — kullanıcı kartı açmadan önce hızlıca özet fikir edinsin.
                    NOT: Duygu durumu/vade rozetleri buradan kaldırıldı — kart açıldığında
                    zaten Sektör/Yön/Vade/Etki Skoru olarak detaylı gösteriliyor, tekrar
                    burada göstermek gereksiz kalabalık yaratıyordu. -->
${(kartAyarlari.aiGorunum !== 'gizle' && kartAyarlari.aiGorunum !== 'sadece_acik') ? `
                    <div class="flex items-start gap-1.5 mt-2 pt-2 border-t border-slate-100 dark:border-slate-800/70">
                        <i class="fa-solid fa-robot text-brand dark:text-blue-400 text-xs mt-0.5 shrink-0"></i>
                        <p class="text-xs leading-relaxed text-slate-600 dark:text-slate-300 ${kartAyarlari.aiGorunum === 'her_zaman_tam' ? '' : 'line-clamp-2'}">
                            <span class="font-bold uppercase tracking-wide text-brand dark:text-blue-400 mr-1">Yapay Zeka Genel Değerlendirmesi:</span>${aYorum || "Bu haber analiz sırasını bekliyor."}
                        </p>
                    </div>` : ''}
                </div>

                <!-- SAĞ: Haber Genel Skoru + açılır ok -->
                <div class="flex items-center gap-3 shrink-0">
                    ${skorHtml}
                    <i class="fa-solid fa-chevron-down text-slate-400 transition-transform ${acik ? "rotate-180" : ""}"></i>
                </div>
            </div>
        </div>

        <div id="detay-${hId}" class="detay-govde ${acik ? "acik" : ""} border-t border-slate-200 dark:border-slate-800">
            <div class="p-3 sm:p-5 space-y-3 sm:space-y-4">

                <!-- YAPAY ZEKA DEĞERLENDİRMESİ: vurgulu, ayrı kutu içinde -->
                <div class="rounded-xl border border-brand/20 dark:border-blue-500/20 bg-brand/5 dark:bg-blue-500/5 p-4">
                    <div class="flex items-center justify-between gap-2 mb-3 flex-wrap">
                        <p class="text-xs font-bold uppercase tracking-wide text-brand dark:text-blue-400 flex items-center gap-1.5">
                            <i class="fa-solid fa-robot"></i>Yapay Zeka Genel Değerlendirmesi
                        </p>
                        ${(mevcutRol === "admin" && ilkPdfUrl) ? `
                        <button id="btnPdfAnalizEt-${hId}" onclick="pdfEkiniAnalizEt(${hId}, event)"
                            class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-brand/10 hover:bg-brand/20 dark:bg-blue-500/10 dark:hover:bg-blue-500/20 text-brand dark:text-blue-400 text-[11px] font-semibold transition"
                            title="Bildirim ekindeki PDF'i de analiz ederek yorumu detaylandır">
                            <i class="fa-solid fa-file-magnifying-glass"></i>PDF Ekini Analiz Et
                        </button>` : ""}
                    </div>

                    <!-- ANALİZ ÖZET SATIRI: Sektör / Yön / Vade / Etki Skoru — sektör bilgisi
                    artık ana sayfada değil, sadece bu detaylı değerlendirme içinde gösterilir -->
                    <div class="grid grid-cols-2 gap-2 mb-3">
                        <div class="rounded-lg bg-white/60 dark:bg-black/20 px-2.5 py-1.5">
                            <p class="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Sektör</p>
                            <p class="text-xs font-bold text-slate-700 dark:text-slate-200 truncate">${kartAyarlari.sektorGoster ? (sekAdi || "Belirtilmedi") : "—"}</p>
                        </div>
                        <div class="rounded-lg bg-white/60 dark:bg-black/20 px-2.5 py-1.5">
                            <p class="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Yön</p>
                            <p class="text-xs font-bold text-slate-700 dark:text-slate-200 truncate">${dDurum || "Bekliyor"}</p>
                        </div>
                        <div class="rounded-lg bg-white/60 dark:bg-black/20 px-2.5 py-1.5">
                            <p class="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Vade</p>
                            <p class="text-xs font-bold text-slate-700 dark:text-slate-200 truncate">${eVadesi || "Bekliyor"}</p>
                        </div>
                        <div class="rounded-lg bg-white/60 dark:bg-black/20 px-2.5 py-1.5">
                            <p class="text-[10px] font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400">Etki Skoru</p>
                            <p class="text-xs font-bold text-slate-700 dark:text-slate-200 truncate">${skor !== null ? `%${Math.abs(skor)} (${skor > 0 ? "Pozitif" : skor < 0 ? "Negatif" : "Nötr"})` : "Bekliyor"}</p>
                        </div>
                    </div>


                </div>

                ${indikatorHtml ? `
                <div>
                    <p class="text-xs font-semibold uppercase tracking-wide text-slate-500 dark:text-slate-400 mb-2">İlgili Göstergeler</p>
                    <div class="flex flex-wrap gap-1.5">${indikatorHtml}</div>
                </div>` : ""}



                ${eklerHtml}

                <div class="flex items-center justify-between flex-wrap gap-2 pt-3 border-t border-slate-100 dark:border-slate-800">
                    <div class="flex items-center gap-2 flex-wrap">
                        ${kapLinki ? `
                        <a href="${kapLinki}" target="_blank" rel="noopener" onclick="event.stopPropagation()"
                           class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-300 dark:border-slate-700 hover:border-brand text-xs transition">
                            <i class="fa-solid fa-arrow-up-right-from-square"></i>KAP Sayfasına Git
                        </a>` : ""}
                        <button onclick="sirketGecmisiniGoster('${hKodu}', event)"
                           class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-300 dark:border-slate-700 hover:border-brand text-xs transition">
                            <i class="fa-solid fa-clock-rotate-left"></i>Geçmiş Bildirimler
                        </button>
                    </div>
                    <div id="admin-aksiyonlar-${hId}" class="${mevcutRol === "admin" ? "" : "js-gizli"} flex items-center gap-2">
                        <button id="btnYenidenAnalizle-${hId}" onclick="haberiYenidenAnalizEt(${hId}, event)"
                            class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-brand hover:bg-brand-hover text-white text-xs font-medium transition">
                            <i class="fa-solid fa-rotate"></i>Bu Haberi Yeniden Analiz Et
                        </button>
                        <button onclick="haberSil(${hId}, event)"
                            class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-rose-400 text-rose-500 hover:bg-rose-500 hover:text-white text-xs font-medium transition">
                            <i class="fa-solid fa-trash"></i>Sil
                        </button>
                    </div>
                </div>
            </div>
        </div>
    </div>`;
}

function detayAcKapat(id) {
    if (acikDetayIdSeti.has(id)) {
        acikDetayIdSeti.delete(id);
    } else {
        acikDetayIdSeti.add(id);
    }
    ekranaHaberleriBas();
}

function formatTarih(tStr) {
    if (!tStr) return "";
    const d = new Date(tStr);
    if (isNaN(d.getTime())) return "";
    return d.toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

// ============================================================
// SAYFALAMA
// ============================================================
function sayfalamaButonlariniOlustur(toplamSayfa) {
    const alan = document.getElementById("sayfalamaKonteyneri");
    if (!alan) return;
    alan.innerHTML = "";
    if (toplamSayfa <= 1) return;

    const isMobile = window.innerWidth < 640;
    const btnStil = "flex-shrink-0 w-8 h-8 flex items-center justify-center rounded-lg border border-slate-300 dark:border-slate-700 text-xs hover:border-brand hover:bg-brand/5 disabled:opacity-40 disabled:cursor-not-allowed";
    const btnAktifStil = "flex-shrink-0 w-8 h-8 flex items-center justify-center rounded-lg bg-brand text-white text-xs font-bold";

    alan.appendChild(sayfaButonuYarat("«", 1, mevcutSayfa === 1, btnStil));
    alan.appendChild(sayfaButonuYarat("‹", mevcutSayfa - 1, mevcutSayfa === 1, btnStil));

    const aralik = isMobile ? 1 : 2;
    const bas = Math.max(1, mevcutSayfa - aralik);
    const son = Math.min(toplamSayfa, mevcutSayfa + aralik);

    if (bas > 1) {
        alan.appendChild(sayfaButonuYarat(1, 1, false, btnStil));
        if (bas > 2) {
            const dots = document.createElement("span");
            dots.textContent = "…";
            dots.className = "px-1 text-slate-400 text-xs self-center";
            alan.appendChild(dots);
        }
    }

    for (let i = bas; i <= son; i++) {
        alan.appendChild(sayfaButonuYarat(i, i, false, i === mevcutSayfa ? btnAktifStil : btnStil));
    }

    if (son < toplamSayfa) {
        if (son < toplamSayfa - 1) {
            const dots = document.createElement("span");
            dots.textContent = "…";
            dots.className = "px-1 text-slate-400 text-xs self-center";
            alan.appendChild(dots);
        }
        alan.appendChild(sayfaButonuYarat(toplamSayfa, toplamSayfa, false, btnStil));
    }

    alan.appendChild(sayfaButonuYarat("›", mevcutSayfa + 1, mevcutSayfa === toplamSayfa, btnStil));
    alan.appendChild(sayfaButonuYarat("»", toplamSayfa, mevcutSayfa === toplamSayfa, btnStil));
}

function sayfaButonuYarat(metin, hedef, disabled, sinif) {
    const b = document.createElement("button");
    b.innerText = metin;
    b.className = sinif;
    b.disabled = disabled;
    if (!disabled) b.onclick = () => sayfayaGit(hedef);
    return b;
}

// ============================================================
// ADMIN GİRİŞ / ÇIKIŞ
// ============================================================
function adminGirisPencerisiniAc() { document.getElementById("modalAdminGiris").classList.remove("js-gizli"); }
function modalKapat(id) { document.getElementById(id).classList.add("js-gizli"); }

async function adminGirisYap() {
    const kullaniciAdi = document.getElementById("txtKullaniciAdi").value.trim();
    const sifre = document.getElementById("txtSifre").value;

    if (!kullaniciAdi || !sifre) {
        popupGoster("Kullanıcı adı ve şifre gereklidir.", "hata");
        return;
    }

    try {
        const yanit = await fetch("/api/Auth/giris", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ kullaniciAdi, sifre })
        });

        if (!yanit.ok) {
            popupGoster("Kullanıcı adı veya şifre hatalı.", "hata");
            return;
        }

        const sonuc = await yanit.json();
        const token = sonuc.token || sonuc.Token;
        localStorage.setItem("token", token);

        modalKapat("modalAdminGiris");
        adminOturumKontrolEt();
        ekranaHaberleriBas();
        popupGoster("Giriş başarılı, hoş geldiniz.", "basarili");
    } catch (hata) {
        console.error(hata);
        popupGoster("Giriş sırasında bir hata oluştu.", "hata");
    }
}


// ============================================================
// YENİ ADMIN PANELİ FONKSİYONLARI
// ============================================================

// --- Kart ayarları localStorage ---
function kartAyarlariyukle() {
    try {
        const saved = localStorage.getItem("kartAyarlari");
        const defaults = {
            aiGorunum: "kapali_acik",
            skorGoster: true,
            sektorGoster: true
        };
        return saved ? { ...defaults, ...JSON.parse(saved) } : defaults;
    } catch { return { aiGorunum: "kapali_acik", skorGoster: true, sektorGoster: true }; }
}

function kartAyarlariniKaydet(ayarlar) {
    try { localStorage.setItem("kartAyarlari", JSON.stringify(ayarlar)); } catch { }
}

// --- Admin paneli aç ---
function adminPanelAc() {
    const modal = document.getElementById("modalAdminPanel");
    if (!modal) return;
    modal.classList.remove("js-gizli");
    adminSekmeAc("genel");
    adminMetrikleriYukle();
    // Mevcut ayarları yansıt
    const ayarlar = kartAyarlariyukle();
    const radios = document.querySelectorAll('input[name="aiGorunum"]');
    radios.forEach(r => { r.checked = r.value === ayarlar.aiGorunum; });
    toggleDurumGuncelle("toggleSkorGoster", ayarlar.skorGoster);
    toggleDurumGuncelle("toggleSektorGoster", ayarlar.sektorGoster);
}

function modalKapat(id) {
    const el = document.getElementById(id);
    if (el) el.classList.add("js-gizli");
}

// --- Sekme geçişi ---
function adminSekmeAc(sekmeAdi) {
    // Tüm panelleri gizle
    document.querySelectorAll(".admin-panel-icerik").forEach(p => p.classList.add("js-gizli"));
    // Tüm sekme butonlarını pasif yap
    document.querySelectorAll(".admin-sekme-btn").forEach(b => {
        b.classList.remove("border-brand", "text-brand", "dark:text-blue-400", "font-bold");
        b.classList.add("border-transparent", "text-slate-500", "font-semibold");
    });
    // Seçili sekmeyi aç
    const panel = document.getElementById("panel-" + sekmeAdi);
    if (panel) panel.classList.remove("js-gizli");
    const btn = document.getElementById("sekme-" + sekmeAdi);
    if (btn) {
        btn.classList.add("border-brand", "text-brand", "dark:text-blue-400", "font-bold");
        btn.classList.remove("border-transparent", "text-slate-500", "font-semibold");
    }
    // Loglar sekmesi açılınca logları yükle
    if (sekmeAdi === "loglar") adminLogarYukle();
}

// --- AI Görünüm değiştir ---
function aiGorunumDegistir(deger) {
    const ayarlar = kartAyarlariyukle();
    ayarlar.aiGorunum = deger;
    kartAyarlariniKaydet(ayarlar);
    ekranaHaberleriBas(); // Kartları yeniden çiz
}

// --- Toggle ayarları ---
function toggleKartAyar(ayarAdi) {
    const ayarlar = kartAyarlariyukle();
    ayarlar[ayarAdi] = !ayarlar[ayarAdi];
    kartAyarlariniKaydet(ayarlar);
    toggleDurumGuncelle("toggle" + ayarAdi.charAt(0).toUpperCase() + ayarAdi.slice(1), ayarlar[ayarAdi]);
    ekranaHaberleriBas();
}

function toggleDurumGuncelle(btnId, aktif) {
    const btn = document.getElementById(btnId);
    if (!btn) return;
    if (aktif) {
        btn.classList.add("bg-brand");
        btn.classList.remove("bg-slate-300", "dark:bg-slate-700");
        btn.querySelector("span").classList.remove("translate-x-0", "left-1");
        btn.querySelector("span").classList.add("right-1");
    } else {
        btn.classList.remove("bg-brand");
        btn.classList.add("bg-slate-300", "dark:bg-slate-700");
        btn.querySelector("span").classList.remove("right-1");
        btn.querySelector("span").classList.add("left-1");
    }
}

// --- Metrikler ---
async function adminMetrikleriYukle() {
    try {
        const token = localStorage.getItem("token");
        const r = await fetch("/api/Haberler?sayfaNo=1&sayfaBoyutu=9999", {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!r.ok) return;
        const data = await r.json();
        const haberler = data.haberler || data.items || data.data || [];
        const toplam = data.toplamAdet || haberler.length;
        const analizEdilen = haberler.filter(h => (h.aiYorum || h.AiYorum || h.yapayZekaYorumu || h.YapayZekaYorumu || "").trim()).length;
        const bekleyen = toplam - analizEdilen;

        const el = id => document.getElementById(id);
        if (el("apToplamHaber")) el("apToplamHaber").textContent = toplam.toLocaleString("tr-TR");
        if (el("apAnalizEdilen")) el("apAnalizEdilen").textContent = analizEdilen.toLocaleString("tr-TR");
        if (el("apBekleyen")) el("apBekleyen").textContent = bekleyen.toLocaleString("tr-TR");
        if (el("apHatali")) el("apHatali").textContent = "0";
        if (el("apGeminiIstek")) el("apGeminiIstek").textContent = "~" + Math.ceil(analizEdilen / 5);
        if (el("apTasarruf")) el("apTasarruf").textContent = (analizEdilen - Math.ceil(analizEdilen / 5)) + " istek";
    } catch (e) {
        console.error("Metrik yükleme hatası:", e);
    }
}

// --- İşlem log çıktısı ---
function islemLogEkle(mesaj, tip = "info") {
    const logDiv = document.getElementById("apIslemLog");
    if (!logDiv) return;
    logDiv.classList.remove("js-gizli");
    const renkler = { info: "text-emerald-400", warn: "text-amber-400", error: "text-rose-400" };
    const ikonlar = { info: "▶", warn: "⚠", error: "✗" };
    const satir = document.createElement("p");
    satir.className = renkler[tip] || "text-emerald-400";
    const zaman = new Date().toLocaleTimeString("tr-TR");
    satir.textContent = `[${zaman}] ${ikonlar[tip] || "▶"} ${mesaj}`;
    logDiv.appendChild(satir);
    logDiv.scrollTop = logDiv.scrollHeight;
}

function butonYukle(btnId, yukleniyor) {
    const btn = document.getElementById(btnId);
    if (!btn) return;
    btn.disabled = yukleniyor;
    if (yukleniyor) {
        btn.dataset.orijinal = btn.innerHTML;
        btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i>';
    } else {
        btn.innerHTML = btn.dataset.orijinal || btn.innerHTML;
    }
}

// --- İşlemler ---
async function adminYeniHaberleriTara() {
    const token = localStorage.getItem("token");
    butonYukle("apBtnTara", true);
    islemLogEkle("KAP taraması başlatılıyor...");
    try {
        const r = await fetch("/api/Haberler/tara", {
            method: "POST",
            headers: { "Authorization": "Bearer " + token, "Content-Type": "application/json" }
        });
        const d = await r.json().catch(() => ({}));
        if (r.ok) {
            islemLogEkle(`Tara tamamlandı. ${d.yeniHaberSayisi ?? d.count ?? "?"} yeni haber bulundu.`);
            await haberleriYukle();
            adminMetrikleriYukle();
        } else {
            islemLogEkle("Tara başarısız: " + (d.message || r.status), "error");
        }
    } catch (e) {
        islemLogEkle("Bağlantı hatası: " + e.message, "error");
    } finally {
        butonYukle("apBtnTara", false);
    }
}

async function adminAiAnalizTetikle() {
    const token = localStorage.getItem("token");
    butonYukle("apBtnAiAnaliz", true);
    islemLogEkle("AI analizi başlatılıyor...");
    try {
        const r = await fetch("/api/Haberler/analiz-tetikle", {
            method: "POST",
            headers: { "Authorization": "Bearer " + token, "Content-Type": "application/json" }
        });
        const d = await r.json().catch(() => ({}));
        if (r.ok) {
            islemLogEkle(`Analiz tamamlandı. ${d.analizEdilenSayisi ?? d.count ?? "?"} haber işlendi.`);
            await haberleriYukle();
            adminMetrikleriYukle();
        } else {
            islemLogEkle("Analiz başarısız: " + (d.message || r.status), "error");
        }
    } catch (e) {
        islemLogEkle("Bağlantı hatası: " + e.message, "error");
    } finally {
        butonYukle("apBtnAiAnaliz", false);
    }
}

async function adminTaraVeAnalizEt() {
    const token = localStorage.getItem("token");
    butonYukle("apBtnTaraAnaliz", true);
    islemLogEkle("Tara + Analiz başlatılıyor...");
    try {
        islemLogEkle("1/2 KAP taranıyor...");
        const r1 = await fetch("/api/Haberler/tara", {
            method: "POST",
            headers: { "Authorization": "Bearer " + token, "Content-Type": "application/json" }
        });
        const d1 = await r1.json().catch(() => ({}));
        if (r1.ok) {
            islemLogEkle(`Tara OK — ${d1.yeniHaberSayisi ?? d1.count ?? "?"} yeni haber`);
        } else {
            islemLogEkle("Tara başarısız: " + (d1.message || r1.status), "warn");
        }
        islemLogEkle("2/2 AI analizi çalışıyor...");
        const r2 = await fetch("/api/Haberler/analiz-tetikle", {
            method: "POST",
            headers: { "Authorization": "Bearer " + token, "Content-Type": "application/json" }
        });
        const d2 = await r2.json().catch(() => ({}));
        if (r2.ok) {
            islemLogEkle(`Analiz OK — ${d2.analizEdilenSayisi ?? d2.count ?? "?"} haber işlendi.`);
        } else {
            islemLogEkle("Analiz başarısız: " + (d2.message || r2.status), "warn");
        }
        await haberleriYukle();
        adminMetrikleriYukle();
    } catch (e) {
        islemLogEkle("Hata: " + e.message, "error");
    } finally {
        butonYukle("apBtnTaraAnaliz", false);
    }
}

// --- Logları yükle ---
async function adminLogarYukle() {
    const token = localStorage.getItem("token");
    const logDiv = document.getElementById("apLogListesi");
    if (!logDiv) return;
    logDiv.innerHTML = '<p class="text-slate-400 text-center py-6 text-xs">Yükleniyor...</p>';
    try {
        const r = await fetch("/api/Loglar", {
            headers: { "Authorization": "Bearer " + token }
        });
        if (!r.ok) { logDiv.innerHTML = '<p class="text-rose-400 text-center py-6 text-xs">Loglar yüklenemedi.</p>'; return; }
        const loglar = await r.json();
        if (!loglar.length) { logDiv.innerHTML = '<p class="text-slate-400 text-center py-6 text-xs">Log bulunamadı.</p>'; return; }
        const renkMap = { "FTL": "text-rose-400", "ERR": "text-rose-300", "WRN": "text-amber-400", "INF": "text-emerald-400", "DBG": "text-slate-400" };
        logDiv.innerHTML = loglar.slice(0, 100).map(l => {
            const lvl = (l.level || l.Level || "INF").substring(0, 3).toUpperCase();
            const zaman = l.timeStamp || l.TimeStamp || l.timestamp || "";
            const msg = l.message || l.Message || l.renderedMessage || l.RenderedMessage || "";
            const renk = renkMap[lvl] || "text-slate-300";
            const tarihStr = zaman ? new Date(zaman).toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }) : "";
            return `<div class="flex gap-2 py-0.5 border-b border-slate-800/50">
                <span class="shrink-0 text-slate-600 w-28">${tarihStr}</span>
                <span class="shrink-0 font-bold w-8 ${renk}">${lvl}</span>
                <span class="text-slate-300 break-all">${msg.substring(0, 200)}</span>
            </div>`;
        }).join("");
    } catch (e) {
        logDiv.innerHTML = `<p class="text-rose-400 text-center py-6 text-xs">Hata: ${e.message}</p>`;
    }
}

function cikisYap() {
    localStorage.removeItem("token");
    adminOturumKontrolEt();
    ekranaHaberleriBas();
    popupGoster("Çıkış yapıldı.", "basarili");
}

// ============================================================
// HABER SİLME (admin)
// ============================================================
async function haberSil(id, event) {
    if (event) event.stopPropagation();
    if (!confirm("Bu bildirimi silmek istediğinizden emin misiniz?")) return;

    try {
        const yanit = await fetch(`${API_URL}/${id}`, {
            method: "DELETE",
            headers: { ...yetkiliBasliklar() }
        });
        if (!yanit.ok) throw new Error(`HTTP ${yanit.status}`);

        popupGoster("Bildirim silindi.", "basarili");
        await verileriBackenddenCek();
    } catch (hata) {
        console.error(hata);
        popupGoster("Silme sırasında hata oluştu.", "hata");
    }
}

// ============================================================
// BİLDİRİM (TOAST)
// ============================================================
function popupGoster(mesaj, durum = "basarili") {
    const el = document.getElementById("popupBildirim");
    el.innerText = mesaj;
    el.className = `fixed bottom-6 right-6 z-[60] px-4 py-3 rounded-lg shadow-lg text-white text-sm font-medium ${durum === "basarili" ? "bg-emerald-600" : "bg-rose-600"}`;
    el.classList.remove("js-gizli");
    setTimeout(() => el.classList.add("js-gizli"), 3500);
}

// ============================================================
// EXCEL DIŞA AKTARMA
// ============================================================
function exceleAktar() {
    if (!ekrandakiHaberler || ekrandakiHaberler.length === 0) {
        popupGoster("Aktarılacak veri bulunamadı.", "hata");
        return;
    }

    const veri = ekrandakiHaberler.map(h => ({
        "Hisse Kodu": h.hisseKodu || h.HisseKodu || "",
        "Şirket": h.sirketAdi || h.SirketAdi || "",
        "Sektör": h.sektorAdi || h.SektorAdi || "",
        "Başlık": h.baslik || h.Baslik || "",
        "Duygu Durumu": h.duyguDurumu || h.DuyguDurumu || "",
        "Etki Vadesi": h.etkiVadesi || h.EtkiVadesi || "",
        "Yorum": h.aiYorumu || h.AiYorumu || "",
        "Tarih": formatTarih(h.yayinlanmaTarihi || h.YayinlanmaTarihi),
        "KAP Linki": h.kapLinki || h.KapLinki || ""
    }));

    const worksheet = XLSX.utils.json_to_sheet(veri);
    const workbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(workbook, worksheet, "KAP Bildirimleri");
    XLSX.writeFile(workbook, `kap-bildirimleri-${new Date().toISOString().split("T")[0]}.xlsx`);
}

// ============================================================
// KPI KARTLARI — artık admin'e özel değil, herkese açık (Genel Özet
// butonuyla yana doğru açılan panelde gösterilir).
// ============================================================
function kpiKartlariniGuncelle() {
    if (!tumHaberler || tumHaberler.length === 0) return;

    const toplam = tumHaberler.length;
    const olumlu = tumHaberler.filter(h => ["Olumlu", "Yüksek Olumlu"].includes(h.duyguDurumu || h.DuyguDurumu)).length;
    const olumsuz = tumHaberler.filter(h => ["Olumsuz", "Yüksek Olumsuz"].includes(h.duyguDurumu || h.DuyguDurumu)).length;
    const notr = tumHaberler.filter(h => ["Nötr", "Etkisiz"].includes(h.duyguDurumu || h.DuyguDurumu)).length;

    document.getElementById("kpiToplamHaber").innerText = toplam;
    document.getElementById("kpiOlumluHaber").innerText = olumlu;
    document.getElementById("kpiNotrHaber").innerText = notr;
    document.getElementById("kpiOlumsuzHaber").innerText = olumsuz;

    kpiKutuVurgulariniGuncelle();
}

// ============================================================
// 🌟 v2.3.0 — GENEL ÖZET KUTUCUKLARI TIKLANABİLİR (Toplam/Olumlu/Nötr/Olumsuz)
// Bir kutucuğa basınca, filtre modalındaki "Yön Filtresi" o tek kategoriye
// daraltılır ve liste anında güncellenir — SPK panelindeki KPI tıklama
// kısayoluyla aynı davranış. "Toplam" tüm yön filtrelerini sıfırlar.
// ============================================================
function kpiFiltreUygula(kategori) {
    if (kategori === "toplam") {
        aktifFiltre.yonler = new Set(["Olumlu", "Notr", "Olumsuz"]);
    } else {
        aktifFiltre.yonler = new Set([kategori]);
    }

    mevcutSayfa = 1;
    ekranaHaberleriBas();
    filtreAktifRozetiGuncelle();
    kpiKutuVurgulariniGuncelle();
}

// Şu anda aktif olan yön filtresine göre ilgili KPI kutucuğunu vurgular —
// kullanıcı hangi kategoriye tıkladığını görsel olarak takip edebilsin.
function kpiKutuVurgulariniGuncelle() {
    const tumYonlerSecili = aktifFiltre.yonler.size === 3;

    const eslesme = {
        kpiKutuToplam: tumYonlerSecili,
        kpiKutuOlumlu: !tumYonlerSecili && aktifFiltre.yonler.has("Olumlu") && aktifFiltre.yonler.size === 1,
        kpiKutuNotr: !tumYonlerSecili && aktifFiltre.yonler.has("Notr") && aktifFiltre.yonler.size === 1,
        kpiKutuOlumsuz: !tumYonlerSecili && aktifFiltre.yonler.has("Olumsuz") && aktifFiltre.yonler.size === 1
    };

    Object.entries(eslesme).forEach(([id, aktif]) => {
        const kutu = document.getElementById(id);
        if (kutu) kutu.classList.toggle("ring-2", aktif) && kutu.classList.toggle("ring-accent", aktif);
    });
}

// ============================================================
// 🌟 v2.3.0 — GÜNLÜK PİYASA SKORU (backend'den, gece 00:00'da hesaplanmış
// olarak çekilir — anlık hesaplanmaz). Bkz. GunlukPiyasaSkoruHesaplamaServisi.
// ============================================================
async function piyasaSkoruBackenddenCek() {
    try {
        const yanit = await fetch(`${API_URL}/piyasa-skoru`);
        if (!yanit.ok) throw new Error(`HTTP ${yanit.status}`);
        const veri = await yanit.json();

        const kutu = document.getElementById("kpiPiyasaSkoru");
        const tarihEtiketi = document.getElementById("kpiPiyasaSkoruTarih");
        if (!kutu) return;

        if (!veri.hesaplandiMi) {
            kutu.innerText = "Hesaplanıyor...";
            if (tarihEtiketi) tarihEtiketi.innerText = "";
            return;
        }

        const yuzde = Math.round(Math.abs(veri.netSkor));
        const yonMetni = veri.yon === "Pozitif" ? "Pozitif" : veri.yon === "Olumsuz" ? "Negatif" : "Nötr";
        kutu.innerText = `%${yuzde} (${yonMetni})`;

        if (tarihEtiketi) {
            const tarih = new Date(veri.tarih);
            const formatli = tarih.toLocaleDateString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric" });
            tarihEtiketi.innerText = `(${formatli} kapanışı)`;
        }
    } catch (hata) {
        console.error("Piyasa skoru çekilemedi:", hata);
    }
}

// ============================================================
// GRAFİK MODALI
// ============================================================
function grafikModalAc() {
    document.getElementById("modalGrafik").classList.remove("js-gizli");
    grafikCiz();
}

function grafikCiz() {
    const ctx = document.getElementById("grafikCanvas");
    const sayilar = {
        "Yüksek Olumlu": 0, "Olumlu": 0, "Nötr": 0, "Etkisiz": 0, "Olumsuz": 0, "Yüksek Olumsuz": 0
    };
    tumHaberler.forEach(h => {
        const d = h.duyguDurumu || h.DuyguDurumu;
        if (d && sayilar.hasOwnProperty(d)) sayilar[d]++;
    });

    if (grafikInstance) grafikInstance.destroy();
    grafikInstance = new Chart(ctx, {
        type: "doughnut",
        data: {
            labels: Object.keys(sayilar),
            datasets: [{
                data: Object.values(sayilar),
                backgroundColor: ["#059669", "#10b981", "#d97706", "#94a3b8", "#f43f5e", "#e11d48"]
            }]
        },
        options: { plugins: { legend: { position: "bottom" } } }
    });
}