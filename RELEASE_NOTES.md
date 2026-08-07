# KlasörKasa 1.0.0

İlk dağıtım sürümü.

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

## Doğrulama

Dağıtım öncesinde 15 otomatik senaryo çalıştırılır. Paket SHA-256 değeri release çıktısında üretilir.

## Sınırlama

EXE henüz Authenticode sertifikasıyla imzalanmamıştır.
