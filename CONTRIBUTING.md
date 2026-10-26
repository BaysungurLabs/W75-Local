# Katkı Rehberi

Katkılarınız için teşekkürler! Hata bildirimi, öneri, belge düzeltmesi ve kod katkısı kabul edilir. Katılmadan önce [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) dosyasını okuyun.

## Derleme

Gerekenler: Windows ve Windows ile gelen .NET Framework 4 C# derleyicisi (`csc.exe`). Başka bir araç gerekmez.

```powershell
powershell -ExecutionPolicy Bypass -File derle.ps1
```

Çıktı `W75Local.exe` dosyasıdır. Yeni bir `.cs` dosyası eklerseniz `derle.ps1` içindeki derleme satırına da ekleyin. Derleyici C# 5 düzeyindedir; daha yeni dil özelliklerini kullanmayın. Kod UTF-8 kodlamalıdır (Türkçe karakterler var).

## Cihaz olmadan deneme

Klavye bağlı olmadan arayüzü şu ortam değişkenleriyle deneyebilirsiniz:

- `W75_CIHAZSIZ=1`: cihaza hiç bağlanmaz.
- `W75_SIM_YAZ=1` (yalnızca `W75_CIHAZSIZ=1` ile): "Cihaza Yaz" ve ilgili komutları klavyeye göndermeden simüle eder.
- `W75_VERI_KLASORU=<klasör>`: verileri `%APPDATA%` yerine başka bir klasörde tutar (gerçek ayarlarınıza dokunmamak için kullanın). Veri klasörünün adı geçmişe uyumluluk için `W75Ayarlayici` olarak kalmıştır.

Test amaçlı diğer değişkenler yalnızca bu iki kipte etkilidir.

## Protokol katmanı: dikkatli olun

`Hid.cs`, `Protocol.cs`, `SocdStore.cs`, `TriggerVerify.cs` ve `DigerTab.cs` klavyeyle konuşan ve ayar verisini oluşturan dosyalardır. Bunlardaki bir hata klavyenin ayarlarını bozabilir. Bu dosyalarda değişiklik yapıyorsanız:

- Değişikliği küçük ve tek amaçlı tutun, pull request'te nedenini ayrıntılı yazın.
- **Gerçek klavyede test edilmesi gerekir.** Cihazsız kipler bu katmanı sınamaz. Hangi cihaz ve firmware ile denediğinizi yazın.
- Tahmin edilen, doğrulanmamış komut göndermeyin; yalnızca gerçek cihazda doğrulanmış komutlar eklenebilir.

## Pull request

1. Depoyu çatallayın (fork) ve ayrı bir dal açın.
2. Değişikliğinizi küçük tutun ve derlemenin uyarısız tamamlandığını kontrol edin.
3. Kullanıcıya görünen bir değişiklik varsa `CHANGELOG.md` dosyasına ekleyin.
4. Pull request şablonunu doldurun.

Kişisel verinizi (kullanıcı adı, dosya yolu, seri numarası) kodda, belgelerde ve ekran görüntülerinde bırakmayın.

## Hata bildirimi

[Issues](https://github.com/BaysungurLabs/W75-Local/issues) sayfasında Issue açarken şablonu kullanın ve şunları yazın:

- Program sürümü ve Windows sürümü
- Klavye modeli ve firmware sürümü
- Sorunu yeniden üretme adımları
- Beklenen ve gerçekleşen davranış
- Cihaz sekmesindeki işlem günlüğünden ilgili satırlar (kişisel bilgileri silin)
- Mümkünse ekran görüntüsü

## Lisans

Katkılarınız [MIT Lisansı](LICENSE) altında yayımlanır.
