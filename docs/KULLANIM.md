# Kullanım kılavuzu

W75 Local, Wraith W75 V2 klavyesinin (`W75HEARGB`) profil, tuş haritası, aktivasyon, ışık, SOCD ve diğer ayarlarını Windows üzerinde yerel olarak kaydeder ve klavyeye yazar. Genel bilgi için [README](../README.md) dosyasına bakın.

> Bağımsız, resmî olmayan bir topluluk projesidir; kullanım sorumluluğu size aittir ([DISCLAIMER](../DISCLAIMER.md)).

## Hızlı başlangıç

1. Klavyenin web yapılandırma aracını ve klavyeyi kullanan diğer uygulamaları kapatın. Cihaz başka bir program tarafından açıksa program bağlanamaz.
2. Klavyeyi USB ile bağlayın ve `W75Local.exe` dosyasını çalıştırın. **Bağlan**'a basın.
3. Soldan bir profil seçin: **Günlük**, **Cyberpunk 2077**, **RPG**, **Valorant**.
4. Ayarları düzenleyin. Değişiklikler otomatik olarak `%APPDATA%\W75Ayarlayici\profiles.json` dosyasına kaydedilir.
5. **Cihaza Yaz**'a basın ve onaylayın. Yazma sonunda (klavye USB'ye geri geldikten sonra) program tuş atamalarını (temel + Fn), aktivasyonu/ölü bölgeyi ve ışığı klavyeden geri okuyup doğrular ve tek bir sonuç kutusu gösterir. Polling ve profil seçimi bu kontrolde yer almaz.

> **Yazma sırasında USB kablosunu çıkarmayın.**
>
> Klavye yazmadan yaklaşık 3 saniye sonra USB'den düşüp yeniden bağlanabilir; bu normaldir. Program doğrulamadan önce bunu bekler. Yazma bittikten sonra hemen **Cihazdan Oku** yapmayın, birkaç saniye bekleyin.
>
> **Yedek önerisi:** **Cihazdan Oku** seçili profilin üzerine yazar. Okumadan önce Cihaz sekmesinden **Yedek Al** yapın.

## Profiller ve klavyedeki numaraları

| Programdaki profil | Klavyedeki profil |
|---|---|
| Günlük | **4. profil** |
| Cyberpunk 2077 | 1. profil |
| RPG | 2. profil |
| Valorant | 3. profil |

Klavye **4 profil** saklar (numaraları 1-4); Günlük klavyenin 4. profilidir. Program klavyeye yalnızca 1-4 numaralarını gönderir. Profil adları yalnızca programda tutulur, klavyeye yazılmaz. Profil adı en çok 32 karakter olabilir.

Klavyedeki profil değiştirme kısayolunun tuşları bu projede belgelenmemiştir. Programdan geçmek için **Bu profili klavyede etkinleştir** düğmesini kullanın; bu düğme yalnızca "şu profile geç" komutunu gönderir, ayar yazmaz.

### Profile tıklayınca klavyede etkinleştir

Profil listesinin altındaki bu kutu varsayılan olarak açıktır ve `pencere.json` içinde hatırlanır. Açıkken başka bir profile tıklamak yalnızca profil geçiş komutunu gönderir. Hızlı art arda tıklamada yalnızca son seçilen profil gider. Cihaz bağlı değilse atlanır; program cihaza kendiliğinden bağlanmaz.

### Profil durum yazıları

- **Cihazda güncel** (yeşil): son başarılı **Cihaza Yaz**'dan beri profil değişmedi.
- **Değişti, Cihaza Yaz yap** (turuncu): son başarılı yazmadan sonra profilde bir şey değişti.
- **Işık cihazda; gerisi: Cihaza Yaz** (turuncu): ışık **Canlı uygula** ile gönderildi, profilin geri kalanı yazılmadı.
- **Cihaza yazılmadı** (gri): bu profil için başarılı bir yazma kaydı yok.

Bir yazma, tuş haritası (temel + Fn) ve aktivasyon/ölü bölge uyuşunca "başarılı" sayılır; ışık okunabildiyse o da uyuşmalıdır. Uyuşmazlıkta program kendiliğinden yeniden yazmaz. Kayıtlar `yazim-durumu.json` dosyasında tutulur ve ayar paketine girmez.

## Hazır profiller ve önerilen ayarlar

İlk kurulumda 4 profil de önerilen ayarlarla oluşturulur. Mesafeler cihazın ölçü birimi olan 0,02 mm'nin katlarıdır. Bunlar kesin kurallar değil başlangıç noktalarıdır; parmak hızınıza ve switch'inize göre **Aktivasyon** sekmesinden değiştirin.

| Profil | W A S D | C Q E X | Diğer tuşlar | Işık |
|---|---|---|---|---|
| Günlük | 2,00 mm, RT kapalı | 2,00 mm, RT kapalı | 2,00 mm, RT kapalı | Statik `#FF5038` |
| Cyberpunk 2077 | 1,20 mm, RT açık (basma 0,30, bırakma 0,30) | 2,00 mm, RT kapalı | 2,00 mm, RT kapalı | Statik `#FF8C00` |
| RPG | 1,50 mm, RT kapalı | 2,00 mm, RT kapalı | 2,00 mm, RT kapalı | Statik `#3A4FE0` |
| Valorant | 0,80 mm, RT açık (basma 0,16, bırakma 0,20) | 1,50 mm, RT kapalı | 1,60 mm, RT kapalı | Statik `#FF0000` |

Parlaklık 1'den başlar (karanlık odada rahat olsun diye); isteyen artırabilir. Renkler bu klavyenin LED yapısına göre gözle dengelenmiştir, ekrandaki renk kodları klavyedeki görünümden farklı olabilir.

**Profili Sıfırla**, seçili profilin aktivasyon, Rapid Trigger, ışık ve parlaklık ayarlarını önerilene, tuş atamalarını varsayılana döndürür. Ölü bölge, hız, yön, arka renk ve profil adı korunur. Sıfırlamadan önce `backups\profiles-sifirlama-oncesi-*.json` otomatik yedeği alınır (alınamazsa sıfırlama yapılmaz). Sıfırlama klavyeye bir şey yazmaz; klavyeye gitmesi için **Cihaza Yaz** gerekir.

AltGr / sağ Alt tuşuna dokunulmaz. Caps Lock fabrika varsayılanında kalır; isterseniz **Tuşlar** sekmesinden başka bir eylem atayabilirsiniz.

## Sekmeler

- **Tuşlar (Keymap):** Temel ve Fn katmanı. Klavyeden veya listeden tuş seçin, sağdaki listeden eylemi seçip **Ata** ya da **Seçili tuşlara** kullanın; **Varsayılan (sıfırla)** atamayı kaldırır. **Tuş yazıları: TR | US** seçici yalnızca ekrandaki yazıları değiştirir (Türkçe Q / İngilizce Q); klavyeye giden veri aynıdır. Eylem listesinde yalnızca standart klavye tuşları vardır.
- **Aktivasyon:** Tüm tuşlar veya seçili tuşlar için mesafe, Rapid Trigger, basma/bırakma hassasiyeti, üst/alt ölü bölge. **Tümüne uygula** ve **Seçili tuşlara** düğmeleri.
- **Polling hızı (Aktivasyon > Polling Rate):** Klavyede polling profile özel değil, cihaz geneli bir ayardır; programda da tüm klavye için tek ayardır (`genel.json`, varsayılan 1 kHz). **Cihaza Yaz polling yazmaz**; yalnızca bu bölümdeki **Klavyeye yaz** ile gönderilir (yaz, 3 sn bekle, geri oku). 1 kHz varsayılan değer olduğu için klavyeden ayrıca doğrulanamaz; kutu yalnızca "yazıldı" der. 2/4/8 kHz için okunan değer eşitse "doğrulandı", değilse uyuşmazlık bildirir ve yeniden yazmaz. **Klavyeden oku** 2/4/8 kHz okursa ve programdakinden farklıysa güncellemek isteyip istemediğinizi sorar (varsayılan Hayır).
- **Işıklandırma:** Mod, parlaklık, hız, renkler ve yön. Her modda yalnızca o modun desteklediği seçenekler görünür. Yön yalnızca Dalga, Radar, Aurora, Hıza Duyarlı ve Çizgilenme modlarında vardır ve ok kartlarıyla seçilir (← → tuşlarıyla da değişir). Renk için düğmeye tıklayıp renk seçebilir ya da kutuya `#RRGGBB` yazıp Enter'a basabilirsiniz; geçersiz girişte kutunun kenarı kırmızı olur. Önizleme canlı güncellenir. Yan ışık şeridi bu sürümde düzenlenmez; cihazdaki değer korunur.
- **SOCD:** Karşılıklı yön tuşları için kuralları okur, düzenler ve cihaza yazar; SOCD'yi açıp kapatır. Kurallar cihaz geneldir ve `socd.json` dosyasında tutulur.
- **Diğer:** Win, Shift+Tab, Alt+Tab ve Alt+F4 kilitleri ve (cihaz destekliyorsa) N Tuş / 6 Tuş modu. Profile bağlı değildir; `diger.json` dosyasına yedeklenir.
- **Cihaz:** Cihaz bilgisi, ham yedek alma/geri yükleme, işlem günlüğü ve **Klasörü Aç**.

## Canlı ışık

Işıklandırma sekmesindeki **Canlı uygula** kutusu, ışık ayarını **Cihaza Yaz**'a basmadan klavyede anında gösterir.

- Varsayılan kapalıdır ve kaydedilmez; program her açılışta kapalı başlar.
- Yalnızca ışık komutu gönderilir; tuş haritası, aktivasyon, SOCD, Diğer ve polling gönderilmez. Değişiklikten 400 ms sonra gönderilir; hızlı değişikliklerde yalnızca son değer gider.
- İki koşulu vardır: cihazın bağlı olması ve seçili profilin klavyede etkin olarak bilinmesi (profile tıklamak, **Bu profili klavyede etkinleştir** ya da **Cihaza Yaz** bunu sağlar).
- Kendiliğinden kapanır: gönderim başarısız olursa, cihaz düşerse, 10 dakika dolarsa ya da 200 gönderime ulaşılırsa. Bu sınırlar klavyenin kalıcı belleğinin gereksiz aşınmasını önlemek içindir. Nedeni günlüğe yazılır.
- Kalıcı kayıt yerine geçmez; kalıcı yazmak için **Cihaza Yaz** kullanın.

## Oyun modu ve kilitler

- Win tuşunun altındaki ışık **sürekli yanıyorsa** oyun modu açıktır; klavyede **Fn+Win** ile açılıp kapanır.
- **Diğer** sekmesindeki kilitler **yalnızca oyun modu açıkken** etkilidir.

## Yedekleme ve taşıma

- **Yedek Al:** Klavyenin tuş haritasını, Fn haritasını, tetikleyicilerini, ölü bölgelerini ve ana ışığını `backups\` altına JSON olarak kaydeder. Polling hızı yedeğe girmez.
- **Yedekten Geri Yükle:** Seçilen yedeği cihaza yazar; mevcut ayarlar değişir.
- **Dışa Aktar / İçe Aktar:** Profilleri JSON olarak dışa/içe aktarır. Tüm ayarları tek zip pakette de dışa/içe aktarabilirsiniz. **İçe Aktar**, **Dışa Aktar** dosyasını ve **Profili Sıfırla** otomatik yedeğini tanır.
- İçe aktarmada hangi profillerin geri yükleneceği sorulur, ardından onay istenir ve geri yüklemeden hemen önce mevcut durumun otomatik yedeği (`profiles-ice-aktarma-oncesi-*.json`) alınır; yedek alınamazsa işlem yapılmaz. Dosya bozuk ya da tanınmıyorsa hiçbir şey değişmez.
- Geri yükleme ve içe aktarma klavyeye hiçbir şey yazmaz; klavyeye gitmesi için profili seçip **Cihaza Yaz** kullanın.
- Profil dosyası bozulursa program `.bozuk-*` uzantılı bir kopya alır ve varsayılan profillerle açılır.

## Veri klasörü

`%APPDATA%\W75Ayarlayici` altında: `profiles.json`, `genel.json`, `socd.json`, `diger.json`, `pencere.json`, `yazim-durumu.json`, `backups\`. Veri klasörünün adı geçmişe uyumluluk için `W75Ayarlayici` olarak kalmıştır. Dosyaları kopyalayarak başka bir bilgisayara taşıyabilirsiniz. `W75_VERI_KLASORU` ortam değişkeni ayarlanırsa veriler o klasörde tutulur (normal kullanımda gerekmez).

## Sınırlamalar

- Makrolar ve yan ışık düzenlemesi yoktur. Eylem listesinde yalnızca standart klavye tuşları vardır; medya, fare, kısayol ve kilit eylemleri yoktur.
- Profilde Fn ataması yoksa yazma sırasında Fn katmanı boş yazılır. Özel Fn atamalarınız varsa önce **Cihazdan Oku** ile profile alın.
- Yalnızca W75 V2 / `W75HEARGB` ve firmware V3.22.07 ile test edilmiştir.
- Firmware güncellemesi yapmaz; bilinmeyen komut göndermez.
- Uygulama kapanırken profiller otomatik kaydedilir.

## Sorun giderme

- **Cihaz bulunamadı:** USB kablosunu kontrol edin, web yapılandırma aracını kapatın, programı yeniden başlatın.
- **Ayar bozuldu:** **Yedekten Geri Yükle** ile son yedeği yükleyin ya da klavyenin web aracındaki fabrika ayarlarına dönün.
- Profil konumu: `%APPDATA%\W75Ayarlayici\profiles.json`; yedekler: `%APPDATA%\W75Ayarlayici\backups\`.
