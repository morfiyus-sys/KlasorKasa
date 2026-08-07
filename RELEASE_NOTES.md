# KlasörKasa 1.1.0

Güvenli hesap silme ve yerel uygulama sıfırlama sürümü.

## Özellikler

- AES-256-GCM tabanlı dosya kasası
- Şifreli metadata ve ID tabanlı blob adları
- Master Key sarma ve parola değiştirme
- Kurtarma anahtarıyla parola sıfırlama
- Windows SID ve NTFS ACL koruması
- Transaction benzeri koruma, açma, kilitleme ve korumayı kaldırma akışları
- Otomatik kilit ve güvenli çıkış
- Beş hatalı giriş sonrası bir dakikalık kilit
- Windows 10/11 uyumlu WPF arayüz
- Self-contained x64 tek dosya yayın
- Ana parola ve açık onay gerektiren **Hesabı Sil** akışı
- Hesap silmeden önce tüm kasaları doğrulanmış biçimde özgün klasörlerine geri getirme
- Tek bir kasa geri getirilemezse hesap silmeyi durduran veri kaybı koruması
- Profil, wrapped Master Key, kurtarma bilgisi, ayarlar, günlükler ve başlangıç kaydını temizleme
- Sonraki çalıştırmada ilk kurulum ekranına dönme

## Doğrulama

Dağıtım öncesinde 18 otomatik senaryo çalıştırılır. Paket SHA-256 değeri release çıktısında üretilir.

## Sınırlama

EXE henüz Authenticode sertifikasıyla imzalanmamıştır.
SSD ve modern dosya sistemlerinde silinen blokların adli yöntemlerle geri getirilemeyeceği garanti edilmez.
