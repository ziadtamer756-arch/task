# Sender Pro — نسخة Windows جديدة

تطبيق Windows Forms بالعربية، مع جهات الاتصال، مكتبة المحتوى، قوالب الرسائل، SMTP، ربط Telegram Bot API وWhatsApp Cloud API، وتقارير محلية. هذا إصدار أولي مستقل، وليس استنساخًا مكتملًا للبرنامج المرفق.

راجع [الوظائف والتشغيل والفجوات المتبقية](docs/windows-implementation.md) و[حصر البرنامج المرجعي](docs/sender-pro-inventory.md).

## التطوير

يتطلب .NET SDK 8.0.425. من جذر المشروع:

```powershell
dotnet build src/SenderPro.Windows/SenderPro.Windows.csproj -c Release
dotnet run --project tests/SenderPro.Checks/SenderPro.Checks.csproj -c Release
```

## إصدار Windows

```powershell
./scripts/package-windows.ps1
```

أو استخدم أمر `dotnet publish` الموثق لتوليد الحزمة دون سكربت. ملف ZIP الناتج في `artifacts/SenderPro-Windows-x64.zip`. فك الضغط ثم شغّل `SenderPro.exe`. لا يحتاج المستخدم النهائي تثبيت .NET.

يضيف CI فحص فتح الشاشات على Windows بثلاثة أحجام ولقطات شاشة في حزمة SenderPro-Windows-UI. تبقى تجربة المستخدم الفعلية ومطابقة البرنامج الأصلي بحاجة للمراجعة. اختبارات المنطق تشمل SMTP على خادم محلي وطلبات Telegram/WhatsApp عبر HTTP بديل داخل الاختبارات. لم تُرسل رسائل إلى الحسابات الإنتاجية. الإرسال الفعلي يحتاج إعدادات الحساب من داخل التطبيق.
