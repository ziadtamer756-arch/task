# Sender Pro reference inventory

This inventory was recovered by static inspection of the uploaded Sender Pro.exe and Sender Pro.pdb. No uploaded executable was run. Names are clues about intended features, not verification that those features work or a recovered copy of their implementation.

## Findings

- Main executable: Windows PE32 GUI, managed .NET assembly.
- File and product version metadata: 58.23.0.0. This differs from the uploaded archive name V4.59; feature/version correspondence is unverified.
- Debug symbols reference 351 unique C# source paths excluding Designer.cs files. Those source files were not supplied.
- Visible string inspection shows protected/obfuscated content. UI details and behavior have not been recovered.
- Dependencies supplied include WinForms UI components, Selenium/Playwright/WebView2, Excel support, SQLite, SMTP, Telegram and Instagram libraries. Dependency presence alone does not verify product capabilities.

## Implementation status

Reference discovery only. No replacement application or matching screens have been implemented yet. Platform choice and visual references remain to be established. Each feature needs behavior, input/output, integration requirements and acceptance checks before claiming parity.

## Source-name inventory

### CheckForUpdate (2)

- `CheckForUpdate\Check.cs`
- `CheckForUpdate\IniParser.cs`

### Client (2)

- `Client\AES.cs`
- `Client\SocketIOHandler.cs`

### CookiesTools (1)

- `CookiesTools\FacebookTools\Comment_Line_Cookies.cs`

### CustomUserControls (7)

- `CustomUserControls\CustomTimer.cs`
- `CustomUserControls\CustomTimer3.cs`
- `CustomUserControls\CustomTimer4.cs`
- `CustomUserControls\CustomTimer5.cs`
- `CustomUserControls\CustomTimer_test.cs`
- `CustomUserControls\CustomTimer_test2.cs`
- `CustomUserControls\UserControl1.cs`

### Dash (4)

- `Dash\DashValues.cs`
- `Dash\DashValues_2.cs`
- `Dash\UserControl1.cs`
- `Dash\UserControl2.cs`

### Dialogs (2)

- `Dialogs\AccountCenter.cs`
- `Dialogs\SupportCenter.cs`

### Emails (1)

- `Emails\Emailsitems\AdvancedEmails.cs`

### FacebookTools (67)

- `FacebookTools\Active friends extraction.cs`
- `FacebookTools\Download_all_photos.cs`
- `FacebookTools\Extract_Member.cs`
- `FacebookTools\Extract_Profile_Data_API.cs`
- `FacebookTools\Extractid2.cs`
- `FacebookTools\FacebookItems\Add_Frindes.cs`
- `FacebookTools\FacebookItems\Chat Group.cs`
- `FacebookTools\FacebookItems\Comment_With_Mention_API.cs`
- `FacebookTools\FacebookItems\Comment_line.cs`
- `FacebookTools\FacebookItems\Comment_line_API.cs`
- `FacebookTools\FacebookItems\Comments_API.cs`
- `FacebookTools\FacebookItems\ExtractCommNumbers.cs`
- `FacebookTools\FacebookItems\Extract_Fans.cs`
- `FacebookTools\FacebookItems\Extract_Followers.cs`
- `FacebookTools\FacebookItems\Extract_Messages_From_Pages.cs`
- `FacebookTools\FacebookItems\Extract_Profile_Contacts_API.cs`
- `FacebookTools\FacebookItems\Extract_data.cs`
- `FacebookTools\FacebookItems\Extract_friends_API.cs`
- `FacebookTools\FacebookItems\Extract_links_related_to_search_word.cs`
- `FacebookTools\FacebookItems\Extract_numbers_from_comments_API.cs`
- `FacebookTools\FacebookItems\Extract_user_Followers.cs`
- `FacebookTools\FacebookItems\Extractid2_data.cs`
- `FacebookTools\FacebookItems\Fillter User.cs`
- `FacebookTools\FacebookItems\Fillter_USERSID.cs`
- `FacebookTools\FacebookItems\Frindes.cs`
- `FacebookTools\FacebookItems\GetPOST_ID.cs`
- `FacebookTools\FacebookItems\Group_data.cs`
- `FacebookTools\FacebookItems\Invite2.cs`
- `FacebookTools\FacebookItems\Login.cs`
- `FacebookTools\FacebookItems\Maessagex.cs`
- `FacebookTools\FacebookItems\MentionTool2.cs`
- `FacebookTools\FacebookItems\Mentionortages.cs`
- `FacebookTools\FacebookItems\Messages_User.cs`
- `FacebookTools\FacebookItems\More Facebook.cs`
- `FacebookTools\FacebookItems\Other tools.cs`
- `FacebookTools\FacebookItems\Page_Extracted.cs`
- `FacebookTools\FacebookItems\People.cs`
- `FacebookTools\FacebookItems\Reactions_API.cs`
- `FacebookTools\FacebookItems\Remove_friend.cs`
- `FacebookTools\FacebookItems\Search.cs`
- `FacebookTools\FacebookItems\Search_API.cs`
- `FacebookTools\FacebookItems\Share_To_Group_API.cs`
- `FacebookTools\FacebookItems\Testtools.cs`
- `FacebookTools\FacebookItems\User_Marketing.cs`
- `FacebookTools\FacebookItems\Watch_Post_Comments_API.cs`
- `FacebookTools\FacebookItems\extarctcomment.cs`
- `FacebookTools\FacebookItems\extract tools.cs`
- `FacebookTools\FacebookItems\fillter.cs`
- `FacebookTools\FacebookItems\marketingx.cs`
- `FacebookTools\FacebookItems\marketingx_data.cs`
- `FacebookTools\FacebookItems\rsender.cs`
- `FacebookTools\FacebookItems\rsender_data.cs`
- `FacebookTools\FacebookItems\test266.cs`
- `FacebookTools\MentionToolsX2.cs`
- `FacebookTools\Selling Publishing.cs`
- `FacebookTools\Selling Publishing_data.cs`
- `FacebookTools\Senscor.cs`
- `FacebookTools\Share_From_Page.cs`
- `FacebookTools\URL_TO_IDS.cs`
- `FacebookTools\delete_posts.cs`
- `FacebookTools\delete_posts_new.cs`
- `FacebookTools\facebook_Rate.cs`
- `FacebookTools\flash_message_id.cs`
- `FacebookTools\flash_message_id_data.cs`
- `FacebookTools\flash_page.cs`
- `FacebookTools\post_scraping.cs`
- `FacebookTools\share.cs`

### Handlers (1)

- `Handlers\Download.cs`

### InstagramTools (37)

- `InstagramTools\Extract_Post.cs`
- `InstagramTools\InstagramItems\API_Extract_Users_ID_From_Direct.cs`
- `InstagramTools\InstagramItems\Boot_Message.cs`
- `InstagramTools\InstagramItems\Bot_Message.cs`
- `InstagramTools\InstagramItems\Bot_Messagev2.cs`
- `InstagramTools\InstagramItems\Comments_And_Mentions_API.cs`
- `InstagramTools\InstagramItems\Extarct.cs`
- `InstagramTools\InstagramItems\ExtractPostLikes.cs`
- `InstagramTools\InstagramItems\Extract_Follow_Following_API.cs`
- `InstagramTools\InstagramItems\Extract_From_Hashtag_Api.cs`
- `InstagramTools\InstagramItems\Extract_From_Profile.cs`
- `InstagramTools\InstagramItems\Extract_Phone numbers.cs`
- `InstagramTools\InstagramItems\Extract_Post_Comments_API.cs`
- `InstagramTools\InstagramItems\Extract_Tools.cs`
- `InstagramTools\InstagramItems\Folloeing_v2.cs`
- `InstagramTools\InstagramItems\Folloeing_v23.cs`
- `InstagramTools\InstagramItems\Follow_Like_Comment.cs`
- `InstagramTools\InstagramItems\Follow_unfollow_API.cs`
- `InstagramTools\InstagramItems\Following.cs`
- `InstagramTools\InstagramItems\Following_user.cs`
- `InstagramTools\InstagramItems\Insta__Auto_emails_sign_up.cs`
- `InstagramTools\InstagramItems\Insta_api.cs`
- `InstagramTools\InstagramItems\Insta_api_Sender.cs`
- `InstagramTools\InstagramItems\Messages_Tools.cs`
- `InstagramTools\InstagramItems\Messagesinsta.cs`
- `InstagramTools\InstagramItems\Share_post.cs`
- `InstagramTools\InstagramItems\Share_postv2.cs`
- `InstagramTools\InstagramItems\Tag_mention.cs`
- `InstagramTools\InstagramItems\Tag_mention2.cs`
- `InstagramTools\InstagramItems\Tag_mention_User.cs`
- `InstagramTools\InstagramItems\Watch_Profile_Comment_Mention_API.cs`
- `InstagramTools\InstagramItems\analysis_tools.cs`
- `InstagramTools\InstagramItems\follow.cs`
- `InstagramTools\InstagramItems\follow_by_sugested.cs`
- `InstagramTools\InstagramItems\login.cs`
- `InstagramTools\InstagramItems\scrap hastag.cs`
- `InstagramTools\InstagramItems\test.cs`

### LinkedinTools (20)

- `LinkedinTools\ExtractGroupMembers.cs`
- `LinkedinTools\LinkedinItems\Data_B2C.cs`
- `LinkedinTools\LinkedinItems\Data_B2ED.cs`
- `LinkedinTools\LinkedinItems\Data_Co_Un.cs`
- `LinkedinTools\LinkedinItems\Data_Frindes.cs`
- `LinkedinTools\LinkedinItems\Data_Google.cs`
- `LinkedinTools\LinkedinItems\Extract_Groups.cs`
- `LinkedinTools\LinkedinItems\Extract_Post_Like_Comm.cs`
- `LinkedinTools\LinkedinItems\Extract_Users.cs`
- `LinkedinTools\LinkedinItems\Follow_Like_Comment.cs`
- `LinkedinTools\LinkedinItems\Follow_up_companies.cs`
- `LinkedinTools\LinkedinItems\Join.cs`
- `LinkedinTools\LinkedinItems\Login.cs`
- `LinkedinTools\LinkedinItems\Marketing.cs`
- `LinkedinTools\LinkedinItems\Messages2x.cs`
- `LinkedinTools\LinkedinItems\Scrap_Members.cs`
- `LinkedinTools\LinkedinItems\Search.cs`
- `LinkedinTools\LinkedinItems\Send_Contact.cs`
- `LinkedinTools\LinkedinItems\extract_users_link.cs`
- `LinkedinTools\LinkedinItems\marketingx.cs`

### LoginSystem (5)

- `LoginSystem\LoginHandler.cs`
- `LoginSystem\LoginInterface.cs`
- `LoginSystem\ProtectLogin.cs`
- `LoginSystem\ProtectLoginWorning.cs`
- `LoginSystem\UserData.cs`

### MainSide (2)

- `MainSide\CloseAside.cs`
- `MainSide\MainSideButtons.cs`

### NewSystem (1)

- `NewSystem\HandleButtonsActions.cs`

### Other_Tools (2)

- `Other_Tools\Text_Editor.cs`
- `Other_Tools\serfclick.cs`

### Pinterest (11)

- `Pinterest\PinterestItems\Create_a_Pin.cs`
- `Pinterest\PinterestItems\Download all photos.cs`
- `Pinterest\PinterestItems\Extract_user.cs`
- `Pinterest\PinterestItems\Generate_Accounts.cs`
- `Pinterest\PinterestItems\Login.cs`
- `Pinterest\PinterestItems\Marketing_User.cs`
- `Pinterest\PinterestItems\extract.cs`
- `Pinterest\PinterestItems\extract_pin_users.cs`
- `Pinterest\PinterestItems\extract_user_follow.cs`
- `Pinterest\PinterestItems\follow_unfollow.cs`
- `Pinterest\PinterestItems\send_messages2.cs`

### ProgramTools (1)

- `ProgramTools\MessageBox.cs`

### Properties (1)

- `Properties\AssemblyInfo.cs`

### RedditTools (5)

- `RedditTools\Login.cs`
- `RedditTools\Search.cs`
- `RedditTools\Top Communities.cs`
- `RedditTools\Up_Vote.cs`
- `RedditTools\publishing.cs`

### Root (8)

- `Invoker.cs`
- `LanguageSystem.cs`
- `MSThread.cs`
- `MainForm.cs`
- `Notes.cs`
- `Program.cs`
- `TEST.cs`
- `WebWait.cs`

### SMS (4)

- `SMS\Message_Box_SMS.cs`
- `SMS\New_SMS.cs`
- `SMS\SMS.cs`
- `SMS\SMS2.cs`

### Sarahah (1)

- `Sarahah\SarahahItems\message.cs`

### SeleniumSystem (8)

- `SeleniumSystem\BulkLoginPlatforms.cs`
- `SeleniumSystem\ChromeDriverUpdate.cs`
- `SeleniumSystem\HandleSelenium.cs`
- `SeleniumSystem\HandleSeleniumData.cs`
- `SeleniumSystem\LoadBrowserDataDialog.cs`
- `SeleniumSystem\SaveBrowserDataDialog.cs`
- `SeleniumSystem\SaveBulkSessions.cs`
- `SeleniumSystem\Tools.cs`

### Settings (10)

- `Settings\Generate_Hashtags.cs`
- `Settings\IniFile.cs`
- `Settings\Settingsitems\Email_Fillter.cs`
- `Settings\Settingsitems\GlobalSettings.cs`
- `Settings\Settingsitems\GoogleRate.cs`
- `Settings\Settingsitems\Google_Contacts.cs`
- `Settings\Settingsitems\HarajRate.cs`
- `Settings\Settingsitems\Text_As_vcf.cs`
- `Settings\Settingsitems\VideoAudioDownloader.cs`
- `Settings\Settingsitems\YouTube_Subscriptions.cs`

### SnapChat (1)

- `SnapChat\Send_Messages.cs`

### SystemsHandler (35)

- `SystemsHandler\AES\AES.cs`
- `SystemsHandler\Constants.cs`
- `SystemsHandler\ContentSystem\AddBulkContent.cs`
- `SystemsHandler\ContentSystem\HandleContent.cs`
- `SystemsHandler\ContentSystem\ManageTheContent.cs`
- `SystemsHandler\CookiesSystem\CookieHandler.cs`
- `SystemsHandler\CookiesSystem\LoadCookies.cs`
- `SystemsHandler\CookiesSystem\PlaywrightHelperForCookieSystem.cs`
- `SystemsHandler\DB\DBHandler.cs`
- `SystemsHandler\DB\DBSettingsHelper.cs`
- `SystemsHandler\DB\DBUserHelper.cs`
- `SystemsHandler\DragAndControl.cs`
- `SystemsHandler\ExcelHandler\ExcelHelpers.cs`
- `SystemsHandler\MessageBoxSystem\MessageBoxForm.cs`
- `SystemsHandler\PlaywrightSystem\HandlePlaywightBrowsersInstallation.cs`
- `SystemsHandler\PlaywrightSystem\HandlePlaywright.cs`
- `SystemsHandler\PlaywrightSystem\HandlePlaywrightData.cs`
- `SystemsHandler\PlaywrightSystem\HandlePlaywrightSessions.cs`
- `SystemsHandler\PlaywrightSystem\PlaywrightHelper.cs`
- `SystemsHandler\RestApiSystem\RestApi.cs`
- `SystemsHandler\SaveSystem\HandleSave.cs`
- `SystemsHandler\ScreenRecorderSystem\ScreenRecorderBox.cs`
- `SystemsHandler\ScreenRecorderSystem\ScreenRecorderHandler.cs`
- `SystemsHandler\SplashScreen\SplashScreen.cs`
- `SystemsHandler\SurveySystem\ShowSurvey.cs`
- `SystemsHandler\TelegramSystem\TelegramCodeDialog.cs`
- `SystemsHandler\TelegramSystem\TelegramHandler.cs`
- `SystemsHandler\TelegramSystem\TelegramUserHandler.cs`
- `SystemsHandler\ThemeSystem\ThemeManager.cs`
- `SystemsHandler\TrialSystem\Trial.cs`
- `SystemsHandler\TrialSystem\TrialTools.cs`
- `SystemsHandler\VideoDownloader\VideoDownloader.cs`
- `SystemsHandler\VpnExtensionSystem\InstallVpnExtension.cs`
- `SystemsHandler\VpnExtensionSystem\VpnExtension.cs`
- `SystemsHandler\ZipArchiveExtensions.cs`

### Threads (3)

- `Threads\Extract_Followers_API.cs`
- `Threads\Normal_Mention.cs`
- `Threads\Th_Extract_comments_API.cs`

### TikTok (14)

- `TikTok\API_Extract_Following_And_Followers.cs`
- `TikTok\API_Extract_Users_From_Search.cs`
- `TikTok\Comments_API.cs`
- `TikTok\Download_videos.cs`
- `TikTok\Extract_Tools.cs`
- `TikTok\Following.cs`
- `TikTok\Like_Comment_follow.cs`
- `TikTok\MEDIA.cs`
- `TikTok\Marketing_Tools.cs`
- `TikTok\Mintion.cs`
- `TikTok\TiktokDownloader.cs`
- `TikTok\Upload_Videos.cs`
- `TikTok\Upload_Videos_Tools.cs`
- `TikTok\auto_click.cs`

### TwitterTools (38)

- `TwitterTools\Extract_Data.cs`
- `TwitterTools\Extract_Data_MultipleAccounts.cs`
- `TwitterTools\Following from List.cs`
- `TwitterTools\MessageBox_Twitter2.cs`
- `TwitterTools\Sencor.cs`
- `TwitterTools\TwitterItems\Auto2_emails_sign_up.cs`
- `TwitterTools\TwitterItems\Auto_emails_sign_up.cs`
- `TwitterTools\TwitterItems\Check_Account.cs`
- `TwitterTools\TwitterItems\Download_all_photos.cs`
- `TwitterTools\TwitterItems\Extarct.cs`
- `TwitterTools\TwitterItems\Extract_From_Profile.cs`
- `TwitterTools\TwitterItems\Fillter.cs`
- `TwitterTools\TwitterItems\Follow_user.cs`
- `TwitterTools\TwitterItems\Followers.cs`
- `TwitterTools\TwitterItems\Likes_Tweets.cs`
- `TwitterTools\TwitterItems\Login.cs`
- `TwitterTools\TwitterItems\Marketing.cs`
- `TwitterTools\TwitterItems\Marketing_User.cs`
- `TwitterTools\TwitterItems\Message_Prov3.cs`
- `TwitterTools\TwitterItems\Message_user.cs`
- `TwitterTools\TwitterItems\MessagingPro.cs`
- `TwitterTools\TwitterItems\MessagingPro2.cs`
- `TwitterTools\TwitterItems\Retwitt.cs`
- `TwitterTools\TwitterItems\Retwittv2.cs`
- `TwitterTools\TwitterItems\Scheduled_tweets2.cs`
- `TwitterTools\TwitterItems\Search1.cs`
- `TwitterTools\TwitterItems\Tag-Mention.cs`
- `TwitterTools\TwitterItems\Tag_User.cs`
- `TwitterTools\TwitterItems\TextBox_Twitter.cs`
- `TwitterTools\TwitterItems\extarct_toolss.cs`
- `TwitterTools\TwitterItems\extract trand.cs`
- `TwitterTools\TwitterItems\extract_followers_following.cs`
- `TwitterTools\TwitterItems\extract_posts_users.cs`
- `TwitterTools\TwitterItems\followv2.cs`
- `TwitterTools\TwitterItems\ok.cs`
- `TwitterTools\TwitterItems\retwitt_user.cs`
- `TwitterTools\TwitterItems\tag_test.cs`
- `TwitterTools\trends24.cs`

### VkTools (10)

- `VkTools\Extract_User.cs`
- `VkTools\Extract_data.cs`
- `VkTools\Extract_friends.cs`
- `VkTools\Extract_members.cs`
- `VkTools\Friend_request.cs`
- `VkTools\Log_In.cs`
- `VkTools\Search_for_Communities.cs`
- `VkTools\Search_for_People.cs`
- `VkTools\Send_messages.cs`
- `VkTools\Tag_mention.cs`

### Whatsapp (19)

- `Whatsapp\Contacts.cs`
- `Whatsapp\Flash_sender_v2.cs`
- `Whatsapp\Groups_data.cs`
- `Whatsapp\Message_Box_Whatsapp.cs`
- `Whatsapp\WhatsappTools\API_Sender.cs`
- `Whatsapp\WhatsappTools\Extarct.cs`
- `Whatsapp\WhatsappTools\Extract User.cs`
- `Whatsapp\WhatsappTools\Extract_X.cs`
- `Whatsapp\WhatsappTools\Extract_links_related_to_search_word.cs`
- `Whatsapp\WhatsappTools\Fillter_User.cs`
- `Whatsapp\WhatsappTools\Filtter X.cs`
- `Whatsapp\WhatsappTools\Flash_fillter.cs`
- `Whatsapp\WhatsappTools\Generators numbers.cs`
- `Whatsapp\WhatsappTools\Sender_User.cs`
- `Whatsapp\WhatsappTools\WhatsappFilter.cs`
- `Whatsapp\WhatsappTools\flash_sender.cs`
- `Whatsapp\WhatsappTools\group X.cs`
- `Whatsapp\WhatsappTools\new_sender.cs`
- `Whatsapp\WhatsappTools\tester.cs`

### X_Plus_Tools (9)

- `X_Plus_Tools\API_Extract_user_info.cs`
- `X_Plus_Tools\API_Tweet_with_hashtag.cs`
- `X_Plus_Tools\Comments_And_Mentions_API.cs`
- `X_Plus_Tools\Follow_UnFollow_Comment_API.cs`
- `X_Plus_Tools\Mention_Tools.cs`
- `X_Plus_Tools\TW_Comments_API.cs`
- `X_Plus_Tools\Tweet_With_Mention_API.cs`
- `X_Plus_Tools\Watch_Post_Comments_API.cs`
- `X_Plus_Tools\Watch_Profile_Comment_Mention_API.cs`

### YoutubeTools (18)

- `YoutubeTools\All_Tools_Youtube.cs`
- `YoutubeTools\Getlike.cs`
- `YoutubeTools\Getlike2.cs`
- `YoutubeTools\Getlike_user.cs`
- `YoutubeTools\YoutubeItems\Hits.cs`
- `YoutubeTools\YoutubeItems\Login.cs`
- `YoutubeTools\YoutubeItems\Soundcloud.cs`
- `YoutubeTools\YoutubeItems\Soundcloud_2.cs`
- `YoutubeTools\YoutubeItems\Youtube.cs`
- `YoutubeTools\YoutubeItems\Youtube_2.cs`
- `YoutubeTools\YoutubeItems\kingdomlikepost.cs`
- `YoutubeTools\YoutubeItems\kingdomlikes.cs`
- `YoutubeTools\YoutubeItems\login_2.cs`
- `YoutubeTools\YoutubeItems\youlikehits.cs`
- `YoutubeTools\addmefast.cs`
- `YoutubeTools\followfast.cs`
- `YoutubeTools\like4like.cs`
- `YoutubeTools\likesplanet.cs`

### obj (1)

- `obj\Release\.NETFramework,Version=v4.7.AssemblyAttributes.cs`
