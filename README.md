# LegacyRun 3.3

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](LICENSE)

## Hazır kurulum paketi

Windows kurulum dosyasını doğrudan indirin:

[LegacyRun-3.3.0.msi](https://github.com/Kucukejderha/LegacyRun/raw/refs/heads/main/LegacyRun-3.3.0.msi)

SHA-256: `EE13E651357A552A12774B3DD061416F4A34C81B415E9A3BC936A28AE0874401`

LegacyRun; domaine bağlı veya bağımsız Windows bilgisayarlarında, yalnızca
yönetici tarafından onaylanan eski uygulamaları standart kullanıcıların her
seferinde yönetici parolası girmeden çalıştırmasını sağlayan bir Windows
masaüstü uygulamasıdır.

## Desteklenen sistemler

- Windows 10 1507 ve sonrası
- Windows 11
- x86 ve x64 işletim sistemleri
- Klasik .NET Framework 4.x/CLR 4

Windows App SDK, WinUI, WebView2, MSIX veya ayrıca .NET 6/8 Desktop Runtime
kurulumu gerektirmez. Windows 10 1507 ile gelen .NET Framework 4.6 üzerinde
çalışabilecek klasik API'ler kullanılır.

## Bileşenler

- `LegacyRun.Admin.exe`: Kullanıcının gördüğü tek yönetim ve başlatma ekranı.
- `LegacyRun.exe`: Yalnızca uygulama kısayollarının kullandığı görünmez çalıştırıcı.
- `Installer.wxs`: WiX Toolset MSI tanımı.

## Güvenlik modeli

1. Domain, kullanıcı adı ve parola yalnızca LegacyRun Yönetimi ekranında girilir.
2. Hesap `LogonUser` ile kaydedilmeden önce doğrulanır.
3. Parola Windows DPAPI `CurrentUser` kapsamıyla şifrelenir.
4. Şifreli veri `HKCU\SOFTWARE\LegacyRun\Settings` altında saklanır.
6. Parola hiçbir loga, komut satırına veya MSI özelliğine yazılmaz.
7. Launcher yalnızca yönetici tarafından HKLM izin listesine eklenen yolları
   çalıştırır.
8. Her uygulama çalıştırılmadan hemen önce SHA-256 ile doğrulanır.
9. Komut kabukları, script host'ları ve yaygın yetki aşma araçları yönetim
   ekranında engellenir.

> Standart kullanıcı kendi oturumunda kullanılan kimlik bilgisini süreç
> belleğinden teorik olarak çıkarabilir. Domain Admin yerine mümkünse yalnızca
> gerekli uygulama ve kaynaklara erişebilen ayrı bir çalıştırma hesabı kullanın.

## Çalıştırma mekanizması

LegacyRun 3.0 servis kullanmaz. Program doğrudan etkileşimli kullanıcı
oturumundan `ProcessStartInfo` ile başlatılır. `UseShellExecute=false`,
`Domain`, `UserName`, salt-okunur `SecureString Password`,
`LoadUserProfile=true` ve uygulamanın kendi klasörü çalışma dizini olarak
ayarlanır. Böylece Session 0 ve masaüstü ACL sorunları oluşmaz.

## Kaynaktan derleme

### Gereksinimler

- Windows 10 veya Windows 11
- Yerleşik `.NET Framework v4.0.30319\csc.exe`
- MSI için WiX Toolset 3.14 portable binaries

### EXE dosyalarını derleme

Normal Komut İstemi açın:

```bat
cd LegacyRun
build.cmd
```

Çıktılar `dist` klasöründe oluşur.

### MSI derleme

WiX 3.14 binary paketini indirin ve bir klasöre çıkartın. `WIX` değişkenini WiX
araçlarının bulunduğu klasöre ayarlayın:

```bat
set WIX=C:\Tools\wix314
build-msi.cmd
```

`LegacyRun-3.3.0.msi` dosyası `dist` altında oluşturulur.

## Manuel kurulum

1. `LegacyRun-3.3.0.msi` dosyasına çift tıklayın.
2. Windows UAC ekranında yönetici onayı verin.
3. **LegacyRun Yönetimi** ekranında **Hesap...** ile `DOMAIN\kullanıcı` ve
   parolayı bir kez kaydedin.
4. Aynı ekranda **Ekle...** ile çalıştırılacak `.exe` dosyalarını onaylayın.
5. Uygulamayı seçip **Başlat** düğmesini kullanın veya uygulamaya özel masaüstü
   kısayolu oluşturun.

## Uygulamaya özel masaüstü kısayolu

1. Tek ana ekran olan **LegacyRun Yönetimi** uygulamasını açın.
2. İzin listesinden bir uygulama seçin.
3. **Masaüstü kısayolu** düğmesine basın.

Kısayol Public Desktop altında oluşturulur ve hedef uygulamanın simgesini
kullanır. Kısayol gerçekte şu çağrıyı yapar:

```text
LegacyRun.exe --launch <uygulama-kimliği>
```

Kullanıcı kısayola çift tıkladığında LegacyRun ana penceresi gösterilmez.
İzin listesi ve SHA-256 doğrulamasından sonra hedef uygulama kayıtlı hesapla
doğrudan başlatılır.

## GPO ile dağıtım

MSI dosyasını bilgisayarların okuyabildiği bir UNC paylaşımına koyun. Group
Policy Management içinde:

`Computer Configuration > Policies > Software Settings > Software installation`

yolundan yeni paket ekleyip **Assigned** seçin. Paket `perMachine` MSI'dır.

Kimlik bilgilerini MSI komut satırında vermeyin; MSI özellikleri ve dağıtım
logları parolayı açığa çıkarabilir. İlk hesap yapılandırmasını hedef bilgisayarda
LegacyRun Yönetimi ile yapın veya kurumunuza özel güvenli bir provisioning
süreci kullanın.

## Güncelleme

Yeni sürümde yalnızca ürün sürümünü artırıp yeni MSI'ı üretin. Sabit
`UpgradeCode` sayesinde yeni MSI:

- eski LegacyRun sürümünü kaldırır,
- eski 2.x servisini durdurup kaldırır,
- Launcher ve yönetim uygulamasını yeniler,
- uygulama izin listesini ve şifreli hesabı korur.

GPO'da yeni MSI paketini eskisinin yükseltmesi olarak tanımlamak veya MSI'a
manuel çift tıklamak yeterlidir.

## Tanılama

Yönetim ekranındaki **Logu aç** düğmesi veya şu dosya kullanılabilir:

`%LOCALAPPDATA%\LegacyRun\LegacyRun.log`

Log; istek kimliği, dosya doğrulama sonucu, hesap adı, token/session bilgisi,
PID ve erken çıkış kodunu içerir. Parola içermez. Dosya 2 MB olduğunda
`LegacyRun.previous.log` olarak döndürülür.

## Kaldırma

Windows Ayarları > Uygulamalar bölümünden LegacyRun'ı kaldırın veya:

```bat
msiexec /x LegacyRun-3.3.0.msi
```

## Lisans

Copyright (C) 2026 LegacyRun contributors.

LegacyRun, **GNU Affero General Public License v3.0**
(`AGPL-3.0-only`) altında yayımlanır. Tam koşullar için [LICENSE](LICENSE)
dosyasına bakın. Program hiçbir garanti verilmeden sunulur.

Güvenlik nedeniyle MSI kaldırma işlemi uygulama izin listesini ve kimlik
bilgilerini otomatik silmez. Tam temizlik gerekiyorsa yönetici olarak şu kayıt
anahtarını ayrıca silin:

`HKLM\SOFTWARE\LegacyRun`
