# Sender Pro — نسخة Windows جديدة

تطبيق Windows Forms بالعربية، مع إدارة جهات الاتصال، قوالب الرسائل، حملات البريد عبر SMTP، تقارير وحفظ محلي. هذا إصدار أولي مستقل، وليس استنساخًا مكتملًا للبرنامج المرفق.

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

لم تُختبر الواجهة تفاعليًا في بيئة Linux؛ يلزم تشغيل الحزمة على Windows للتحقق من العرض والتفاعل. اختبارات المنطق تشمل SMTP على خادم محلي فقط؛ استخدام خادم حقيقي يحتاج حسابًا وإعدادات مناسبة.
