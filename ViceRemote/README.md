# Vice Remote

Android app with buttons for Vice: TV and soundbar power, soundbar and woofer volume (hold to keep going), night mode, picture mode, PC media keys and volume, the sleep timer, and lock / sleep / bed time / shut down for the PC. It shows what Vice thinks the TV and soundbar are doing and refreshes every few seconds while it's open.

It talks to Vice's phone API over Tailscale. Setting up the PC side is in the [main README](../README.md#phone-control-with-vice-remote).

## Build and install on your phone

1. Install [Android Studio](https://developer.android.com/studio).
2. **File > Open** and pick this `ViceRemote` folder. Let Gradle sync. If Android Studio offers to update the Android Gradle plugin or library versions, that's fine to accept.
3. On the phone, turn on Developer options and USB debugging, plug it in, and press **Run**.
4. In the app, enter the PC address, port and pairing code from Vice's **Phone > Show pairing details**.

Code layout:

- `data/ViceClient.kt` makes the HTTP calls and turns failures into readable messages
- `data/Commands.kt` lists every button's command
- `RemoteViewModel.kt` holds the state and sends commands
- `ui/RemoteScreen.kt` and `ui/SettingsScreen.kt` are the screens

`./gradlew test` runs the client tests against a fake Vice.

## Publishing to Google Play

1. **Make an upload key** (once, and keep it safe; you need it for every update):
   ```
   keytool -genkeypair -v -keystore upload.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload
   ```
2. **Create `keystore.properties`** in this folder (it's git-ignored):
   ```
   storeFile=upload.jks
   storePassword=...
   keyAlias=upload
   keyPassword=...
   ```
3. **Build the bundle**: `./gradlew bundleRelease`. The file is `app/build/outputs/bundle/release/app-release.aab`.
4. **Play Console** ([play.google.com/console](https://play.google.com/console)): a developer account costs a one-off US$25. Create an app called "Vice Remote", turn on Play App Signing, and upload the `.aab`.
5. **Store listing** needs a 512×512 icon, a 1024×500 feature graphic, at least two phone screenshots, and short and full descriptions.
6. **Policy forms**:
   - *Privacy policy URL*: required. A short page saying the app stores your PC's address and pairing code on the phone only and sends them only to your own PC is enough.
   - *Data safety*: the app collects and shares no data.
   - *Content rating*: fill in the questionnaire (utility, no user content).
   - *Target audience*: 18+ is simplest.
7. **Testing track**: new personal developer accounts must run a closed test with at least 12 testers for 14 days before they can publish to production. If the app is only for you, an **internal testing** track (up to 100 testers, no review wait) is enough and skips that rule.

Before each new upload, raise `versionCode` (and usually `versionName`) in `app/build.gradle.kts`.

## Notes

- The app uses plain HTTP to Vice. That's deliberate: traffic goes through Tailscale, which encrypts it end to end. `res/xml/network_security_config.xml` allows it.
- Library versions in `app/build.gradle.kts` were current when this was written. Android Studio's suggested upgrades are safe to take.
- Play requires new apps and updates to target a recent Android version. If the console rejects the upload over `targetSdk`, raise `compileSdk` and `targetSdk` to the version it names.
