# Güvenlik Politikası

## Güvenlik açığı bildirimi

Bir güvenlik açığı bulduysanız lütfen **herkese açık bir Issue açmayın**. Bunun yerine GitHub'ın **özel güvenlik açığı bildirimi** özelliğini kullanın: deponun **Security** sekmesinde (https://github.com/BaysungurLabs/W75-Local/security) **Report a vulnerability** düğmesi varsa onu seçin. Düğme görünmüyorsa, ayrıntı içermeyen bir Issue açıp özel bir iletişim yolu isteyin; açığın ayrıntılarını Issue'ya yazmayın.

Bildirimde şunlar yardımcı olur: program sürümü, Windows sürümü, adımlar ve olası etki. Bu proje gönüllülerce, en iyi çaba esasıyla sürdürülür; yanıt süresi için söz verilemez.

## Desteklenen sürümler

Yalnızca en son sürüm desteklenir.

## Güvenlik modeli

- **Ağ erişimi yoktur.** Program internete bağlanmaz: güncelleme denetimi, telemetri, hesap ya da uzak sunucu yoktur. Kaynak kodda ağ kullanımı bulunmaz ve derlenmiş dosyada ağ kütüphanelerine başvuru yoktur.
- **Yalnızca yerel dosyalar ve USB (HID).** Program klavye ile USB üzerinden ve kendi veri klasörüyle çalışır.
- **Yönetici izni gerekmez.**
- **Yerel veri konumu:** `%APPDATA%\W75Ayarlayici` (profiller, polling, SOCD, Diğer ayarlar, pencere konumu, yazım durumu ve `backups\` yedekleri). Bu dosyalar düz JSON'dur; parola ya da kişisel hesap bilgisi içermez.
- **Yalnızca doğrulanmış komutlar:** Program klavyeye yalnızca bilinen, test edilmiş komutları gönderir; firmware güncellemesi yapmaz.
- **Kod imzası yoktur.** İndirdiğiniz dosyanın doğruluğunu [Releases](https://github.com/BaysungurLabs/W75-Local/releases) sayfasındaki SHA256 değeriyle denetleyin (bkz. [README](README.md)).
