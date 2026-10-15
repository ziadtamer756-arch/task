# Uploaded Sender Pro: static reference analysis

This report records evidence from the uploaded `Sender Pro.exe`, `Sender Pro.pdb`, and `Sender Pro.exe.config`. The original executable was **not run**. No licensing, protection, or resource encryption was bypassed, and no credential values were printed or copied. This is a UI/feature discovery aid, not a claim that any original feature works or that the replacement has parity.

## Important evidence limitation

The executable's CodeView debug identifier is `a695b78a-5c49-4739-8256-5ce1d3973197`, age 1. The uploaded PDB identifier is `d0e50aba-bae2-4a6e-a92b-9372b017c4ec`, age 1. **They do not match.** Therefore the PDB cannot be treated as the exact debug symbols for this executable. It may come from another build. There are 358 shared original type/module names between the executable's readable metadata and the PDB, which supports a relationship but does not resolve the mismatch.

Evidence labels below:

- **EXE metadata:** readable original type, method, and field/control names in the managed executable. Names establish that corresponding elements exist in its metadata, not their runtime effect.
- **PDB symbols:** managed method records grouped by their original type/module. These may describe a different build.
- **Config:** XML structure, runtime target, and setting names only. Account/password/serial values were deliberately omitted.

The EXE contains many generated/obfuscated methods and seven manifest resources, most with non-descriptive names. Screen labels, control positions, colors, assets, API payloads, validation rules, and runtime results have not been recovered. UI visual matching still needs visual evidence.

## Runtime and application shell

- Config declares CLR `v4.0`, targeting **.NET Framework 4.7**. The EXE is a Windows GUI PE32 managed assembly and references WinForms, Guna UI, and Bunifu Charts.
- `Sender_Pro.MainForm` has header, footer, body, sidebar, account-center and login panels, minimize/exit controls, and a `DarkModeSwitch` in EXE metadata. PDB adds a `CloseAside` method and sidebar drag/paint handlers.
- `MainSideButtons` has `menuItems`; PDB names include `LoadMenuItems`, `CreateBTN`, `CreateSubPanel`, `HandleSubMenuStatus_Click`, and `HandleAllSubMenuStatus`. This supports nested navigation as a design reference, but does not establish its visible menu order.
- `LanguageSystem` has a `language` field and PDB methods `SwitchEnglish` and `SwitchArabic`. Config has `lang` and `Theme` setting names. `ThemeManager` has theme-change/load/use methods and separate coloring methods for grids, text boxes, lists, buttons, check boxes, and charts in PDB symbols.
- `DashValues` / `DashValues_2` contain message-count fields for Facebook, WhatsApp, Instagram, Telegram, Twitter, LinkedIn, Pinterest and VK in EXE metadata. These are counter declarations, not evidence that all those channels send successfully.

## Workflows supported by named controls and handlers

| Area | Concrete evidence | What remains unknown |
| --- | --- | --- |
| WhatsApp sender variants | EXE types `API_Sender`, `flash_sender`, `Flash_sender_v2`, `new_sender`, `group_X`, and `Sender_User`. Several contain recipient list/grid fields, `TextPath`, `PhotoPath`, `time`, `random_time`, numeric controls, status labels, a custom timer and start handlers. `flash_sender` also declares `MaxRows`. | Exact differences between variants; official API vs browser behavior; field captions and import mapping; media rules; session handling; actual send results. A type named `API_Sender` alone does not identify an API provider. |
| WhatsApp filtering | EXE types `WhatsappFilter`, `Flash_fillter`, `Filtter_X`, and `Fillter_User`. `WhatsappFilter` has `CountCells`, a stop field, a grid, numeric field and result labels. `Flash_fillter` has several list-box fields and labels. PDB contains `WhatsAppFilterStart`. | Meaning of each output list, invalid-number rules, result statuses, required login, and whether remote number checking works. |
| WhatsApp contacts and groups | EXE `Contacts` and `Groups_data` have list/grid fields; `Groups_data` has `Linkslist` and `Data_Ex`. `Extract_User` has a link list, grid, list box, numeric input and track bar. | No reliable semantic names for the numbered grid columns; exact extraction inputs/outputs and supported group permissions cannot be determined. |
| Number generation | EXE `Generators_numbers` declares `RandomString`, `GenrateNumbers`, numeric inputs, a text input and a list box. | Country/prefix, length, range, randomness and output rules are unverified. Do not copy assumptions into a specification. |
| Telegram account/session manager | EXE `TelegramHandler` declares `ApiPhoneNumber`, `ApiHash`, `ApiId`, `SessionFilePath`, `SessionsPreview`, `LoginAccount`, `UseSelected`, `DeleteSelected`, `ForceRelogin`, account counts and load-session controls. It has selection, load, delete, use and form-closing handlers. `TelegramCodeDialog` has a code field and login handler. PDB adds `ShowLoginSession`, `ShowUsedSessions`, `ChangeSessionPath_Click` and account models with API ID/hash/phone, session name/path, connected state and client. | This establishes an account/session-management surface. It does **not** establish a Telegram message sender, bot-token flow or exact authentication implementation. User API credentials and bot tokens are different integration choices. |
| Reusable content library | EXE `HandleContent` has add/use/edit/delete/bulk-add controls, tags, preview and count. EXE `AddBulkContent` has file selection, separator, content preview/count and tag fields. `ManageTheContent` has content name/text/tag, add/delete media, media list/count, max media/image/video sizes and save controls. PDB models include content label/save path, random-content/media choices and image/video accessors. | Actual size limits, supported formats, tag syntax, placeholder expansion, separator escaping, ordering and persistence format. |
| Email campaign editor | EXE `AdvancedEmails` has an SMTP-list button, a six-column account grid, recipient list/count, company/subject/message/custom-style fields, custom-style toggle, send-all fields, start/stop/pause-resume handlers, save-log control and timer. | Six grid column headers are numbered and their meanings cannot be recovered reliably. TLS/authentication rules, scheduling, HTML/template syntax, SMTP rotation and error handling are unverified. |
| Shared table export | EXE `HandleSave` has output path, separator, headers, selected-column panel and grid fields. PDB adds `HandlerCheckHeaders`, `defaultSave` and `excelSave`. | Exact file formats, delimiter/encoding defaults and selected-column semantics. |
| Excel import | EXE and PDB declare `ExcelHelpers.ReadExcel`; EXE references ExcelDataReader, ExcelDataReader.DataSet and SpreadsheetLight. | Contact import column names, worksheet selection and normalization rules are **not** recoverable from these declarations. |
| Global settings | EXE `GlobalSettings` has minimum/maximum numeric fields, keywords, random-keyword toggle, save control and `Global_GetThread`, `Global_GetMinValue`, `Global_GetMaxValue`, `Global_GetDelay` accessors. PDB adds timing-trackbar handlers. | Units, limits, persistence defaults and what each channel does with these settings. |
| Browser sessions | EXE `SaveBrowserDataDialog` has session filename/folder and save controls; `LoadBrowserDataDialog` has saved-file list, counts, delete and load controls. `HandlePlaywrightSessions` has session preview, counts, filename, start-browser and save controls. PDB has session-save and expiry methods. | Profile format, browser versions, compatibility and exact data stored. Browser-session storage must be treated as sensitive. |
| Cookie selection | EXE `LoadCookies` has load/delete/use controls and preview/count. PDB has enable/disable and selected-cookie-use methods. | Supported cookie formats, expiry behavior and which platform operations consume them. |
| Screen recording | EXE `ScreenRecorderBox` has save-path, file-selection and start-recording controls. | Capture backend, recording format, audio support and availability. |

## Missing versus merely undeclared behavior

The replacement initially covered email and local contact/template management. The reference has additional **declared UI surfaces** for WhatsApp, account/session management, reusable content/media, platform tools and nested navigation. These deserve explicit coverage in a parity checklist. Adding their names to a menu does not implement them, and a disabled/incomplete tool should be identified honestly in the application.

The original filename inventory in `sender-pro-inventory.md` remains useful for categorizing reference areas. Its 351 source paths came from the mismatching PDB and should be read with the limitation above. Source filenames, dependency names and counter fields do not establish valid service integrations or original success rates.

## Implementation decisions the evidence cannot supply

1. Exact original appearance: screenshots/video are needed for dimensions, typography, screen layouts, icons, visible labels and navigation order.
2. A definitive behavioral specification: examples of inputs and expected outputs are needed for each tool, especially differently named sender/filter variants.
3. Live-channel configuration: no original credentials should be reused from uploaded files. Each replacement integration needs the user's intended account/provider and securely supplied configuration.
4. Service compatibility: no remote login, message send, scraping, or other account action was attempted during this static review.
5. Feature parity: Windows UI checks and successful build/tests of the replacement cannot verify matching behavior of the original program.

## Reproducibility of this audit

The EXE was inspected using `pefile` / `dnfile` from Python packages installed under `/tmp`; parsing never loaded the uploaded assembly into a CLR. The PDB was read as MSF 7.0 data, using its DBI module table and managed-procedure symbol names. Config was parsed as XML while omitting values. Temporary raw symbol/metadata summaries are under `/tmp`, outside the repository. No original files, repository application code, manifests, tests or lockfiles were modified by this audit.
