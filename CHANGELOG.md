# Değişiklik Günlüğü

Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) yapısına, sürümleme [Semantic Versioning](https://semver.org/lang/tr/) yaklaşımına dayanır.

## [1.0.0] - 2026-10-26

İlk herkese açık sürüm.

### Eklendi
- Wraith W75 V2 (`W75HEARGB`) için yerel ayar programı (Windows, .NET Framework 4); tarayıcı gerekmez, ağ erişimi yoktur.
- 4 profil (Günlük, Cyberpunk 2077, RPG, Valorant), önerilen ayarlar ve **Profili Sıfırla** (önce otomatik yedek).
- Tuş haritası (temel + Fn katmanı), TR/US tuş yazısı gösterimi.
- Aktivasyon: tuş başına ya da çoklu seçimle mesafe, Rapid Trigger, üst/alt ölü bölge, grafikli düzenleme.
- 15 ışık modu; renk (`#RRGGBB`), parlaklık, hız, yön ve canlı önizleme; isteğe bağlı **Canlı uygula**.
- Polling hızı (1/2/4/8 kHz), tüm klavye için tek ayar.
- SOCD kuralları ve Diğer ayarlar (Win, Shift+Tab, Alt+Tab, Alt+F4 kilitleri; N tuş / 6 tuş modu).
- **Cihaza Yaz** sonrası otomatik doğrulama (tuş atamaları, aktivasyon, ışık) ve tek sonuç kutusu.
- Cihaz yedeği, profil dışa/içe aktarma, tüm ayarlar için zip paket.
- Koyu tema (pencereler ve iletişim kutuları).
- Hata dayanıklılığı: salt okunur veri klasörü, bozuk ayar dosyaları, klavye takılı değilken anlaşılır Türkçe mesajlar; aynı ayar klasörüyle ikinci kopya açılmasını engelleme; profil adı en çok 32 karakter.

### Bilinen sınırlar
- Arayüz yalnızca Türkçe; yalnızca Wraith W75 V2, firmware V3.22.07 ile test edildi.
- Makro, yan ışık şeridi düzenleme ve firmware güncellemesi yok.
