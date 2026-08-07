# KlasörKasa

KlasörKasa, Windows 10 ve Windows 11 için ERDSoft tarafından geliştirilen yerel klasör kasası uygulamasıdır. Seçilen klasörleri şifreli kasa dosyalarına dönüştürür; doğru ana parola olmadan içerik okunamaz.

## Temel güvenlik mimarisi

- Dosyalar için AES-256-GCM ve benzersiz nonce
- Kullanıcı parolasından PBKDF2-HMAC-SHA256 ile türetilen anahtar şifreleme anahtarı
- Rastgele 256-bit Master Key ve parola değişiminde yalnızca yeniden sarma
- Şifreli dosya adı, yol ve metadata
- Windows kullanıcı SID sahiplik kontrolü
- Kasa alanında sahip ve SYSTEM odaklı NTFS ACL
- Hidden ve System öznitelikleriyle Explorer görünürlüğünü azaltma
- `Encrypt → Verify → Commit → Delete plaintext` işlem sırası
- İsteğe bağlı 256-bit kurtarma anahtarı
- Beş hatalı girişten sonra kalıcı bir dakikalık giriş kilidi

> Gizlilik güvenlik değildir. Bir Administrator kasa dosyalarını bulabilir veya kopyalayabilir; ancak doğru KlasörKasa parolası ya da kurtarma anahtarı olmadan şifreli içeriği okuyamamalıdır.

## Sistem gereksinimleri

- Windows 10 sürüm 2004 veya sonrası ya da Windows 11
- x64 işlemci
- NTFS, ACL korumasının tam uygulanması için önerilir

Yayın paketi self-contained olduğundan ayrıca .NET kurulumu gerekmez.

## Kullanım

1. `KlasorKasa.exe` dosyasını çalıştırın.
2. İlk açılışta güçlü bir ana parola oluşturun.
3. Gösterilen kurtarma anahtarını çevrimdışı ve güvenli bir yere kaydedin.
4. **Yeni Kasa** ile korunacak klasörü seçin.
5. İşiniz bittiğinde kasayı kilitleyin veya **Programdan Güvenle Çık** seçeneğini kullanın.

Kurtarma anahtarı ve ana parola birlikte kaybedilirse şifreli dosyalar kurtarılamaz.

### Hesabı silme

**Güvenlik → Hesabı Sil** işlemi ana parolayı yeniden doğrular. Önce tüm kasaların korumasını kaldırarak klasörleri özgün konumlarında normal kullanıma açar. Tek bir kasa bile güvenli biçimde geri getirilemezse hesap profili silinmez. Başarılı işlemden sonra Master Key bellekten temizlenir; profil, kurtarma bilgisi, ayarlar, günlükler, kasa kayıtları ve Windows başlangıç kaydı kaldırılır. Sonraki açılışta ilk kurulum ekranı gösterilir.

Uygulama yerel hesap dosyalarını silmeden önce üzerlerine yazmayı dener. SSD aşınma dengelemesi ve dosya sistemi davranışı nedeniyle adli veri kurtarmaya karşı fiziksel silme garantisi verilemez.

## Kaynaktan derleme

```powershell
dotnet restore KlasorKasa.sln
dotnet build KlasorKasa.sln -c Release -p:Platform=x64
dotnet run --project KlasorKasa.Tests/KlasorKasa.Tests.csproj -c Release -p:Platform=x64
dotnet publish KlasorKasa.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -p:Platform=x64
```

Tek komutla doğrulanmış dağıtım paketi üretmek için:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Publish-Release.ps1
```

## Veri konumu

Çalışma zamanı verileri `%LOCALAPPDATA%\KlasorKasa` altında tutulur. Bu dizindeki profil, kasalar, ACL yedekleri ve loglar kaynak kod deposuna eklenmemelidir.

## Bilinen sınırlamalar

- Bu sürüm imzalı bir Authenticode sertifikası içermez; Windows SmartScreen ilk çalıştırmada uyarı gösterebilir.
- Açık kasada plaintext dosyalar çalışma klasöründe bulunur ve NTFS ACL ile sınırlandırılır. Açık kasa kullanılmadığında kilitlenmelidir.
- Yönetici hesapları şifreli blobları bulabilir, sahiplik alabilir ve kopyalayabilir; içerik güvenliği kriptografiye dayanır.
- E-posta ile parola kurtarma yoktur; çevrimdışı kurtarma anahtarı kullanılır.
- SSD üzerinde silinen hesap dosyalarının fiziksel olarak geri getirilemez olduğu garanti edilemez; uygulama düzeyinde profil ve anahtar erişimi kaldırılır.

Güvenlik bildirimi için [SECURITY.md](SECURITY.md) dosyasına bakın.

## Lisans

KlasörKasa, [MIT Lisansı](LICENSE) ile yayımlanmaktadır.

© 2026 ERDSoft. Ürün adı: KlasörKasa.
