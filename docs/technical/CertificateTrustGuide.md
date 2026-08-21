# Local HTTPS Certificate Trust Guide

This guide provides step-by-step instructions for establishing browser trust for AI Agent Hub's local self-signed TLS/HTTPS certificate on Windows, macOS, and Linux, eliminating invalid certificate warnings (`NET::ERR_CERT_AUTHORITY_INVALID`) in Google Chrome, Microsoft Edge, and Mozilla Firefox.

---

## Background & Certificate Details

* **Certificate Location:** `%LocalAppData%\AIAgentHub\Certs\server.pfx` (e.g. `C:\Users\<User>\AppData\Local\AIAgentHub\Certs\server.pfx`)
* **Default PFX Password:** `AIAgentHubLocalTlsCertPassword2026!`
* **Subject Alternative Names (SANs):** `localhost`, `127.0.0.1`, `::1`, your machine hostname, and all active local network interface IP addresses.
* **Default Listening Port:** `5432` (`https://localhost:5432`)

---

## Option 1: PowerShell Command (Fastest for Windows)

Open a **PowerShell** prompt (administrator privileges are not required when importing to `CurrentUser\Root`):

```powershell
Import-PfxCertificate -FilePath "$env:LOCALAPPDATA\AIAgentHub\Certs\server.pfx" -CertStoreLocation Cert:\CurrentUser\Root -Password (ConvertTo-SecureString "AIAgentHubLocalTlsCertPassword2026!" -AsPlainText -Force)
```

1. When prompted by the **Windows Security Warning** dialog (*"Do you want to install this certificate?"*), click **Yes**.
2. Fully restart Google Chrome or Microsoft Edge.
3. Navigate to [https://localhost:5432](https://localhost:5432). The connection will now display with a valid lock icon.

---

## Option 2: Directly via Chrome or Edge Browser UI

1. Open [https://localhost:5432](https://localhost:5432) in Chrome or Edge.
2. Click the **"Not secure"** warning badge on the left side of the address bar.
3. Select **"Certificate is not valid"** (or click the certificate viewer icon).
4. In the certificate dialog, navigate to the **Details** tab and click **Export...**.
5. Save the file to your computer (e.g. `agent-hub.crt` or `server.cer`).
6. Double-click the exported file in Windows Explorer and click **Install Certificate...**.
7. In the Certificate Import Wizard:
   * Select **Store Location:** `Current User` &rarr; click **Next**.
   * Select **"Place all certificates in the following store"** &rarr; click **Browse...**.
   * Choose **Trusted Root Certification Authorities** &rarr; click **OK**.
8. Click **Next** &rarr; **Finish**, and confirm **Yes** on the Windows Security Warning.
9. Restart your browser.

---

## Option 3: Windows Certificate Manager (`certmgr.msc`)

1. Press `Win + R`, type `certmgr.msc`, and press **Enter**.
2. In the left navigation pane, expand **Trusted Root Certification Authorities** &rarr; right-click the **Certificates** sub-folder &rarr; select **All Tasks** &rarr; **Import...**.
3. Click **Next**, then click **Browse...**.
4. In the file explorer dialog, change the file type dropdown in the lower-right corner to **Personal Information Exchange (*.pfx; *.p12)**.
5. Navigate to `%LocalAppData%\AIAgentHub\Certs` and select `server.pfx`.
6. Enter the password: `AIAgentHubLocalTlsCertPassword2026!` and click **Next**.
7. Ensure **"Place all certificates in the following store: Trusted Root Certification Authorities"** is selected.
8. Click **Next** &rarr; **Finish**, then confirm **Yes** on the security warning.
9. Restart Chrome or Edge.

---

## Option 4: macOS / Linux Clients (LAN Access)

### macOS (Keychain Access)
1. Copy the public certificate (`server.cer` or export from browser) to your Mac.
2. Open **Keychain Access** (`/Applications/Utilities/Keychain Access.app`).
3. Drag and drop the certificate into the **System** or **login** keychain.
4. Double-click the imported certificate, expand the **Trust** section, and set **"When using this certificate"** to **Always Trust**.
5. Restart your browser.

### Linux (Debian / Ubuntu / WSL)
1. Export the public certificate into `.crt` format (e.g. `server.crt`).
2. Copy it to `/usr/local/share/ca-certificates/`:
   ```bash
   sudo cp server.crt /usr/local/share/ca-certificates/aiagenthub.crt
   sudo update-ca-certificates
   ```
3. For Chrome / Chromium on Linux, you can also import it via `chrome://settings/certificates` under **Authorities**.

---

## Troubleshooting & Verification

* **Certificate Mismatch Error:** Ensure your browser URL matches one of the certificate's SANs (e.g. `https://localhost:5432`, `https://127.0.0.1:5432`, or `https://<computer-name>:5432`).
* **Browser Still Showing Warning After Import:** Chrome caches certificate validation states per session. Close all Chrome windows or open an Incognito window to force certificate re-evaluation.
* **Firefox Specific:** Firefox maintains its own certificate store by default. To make Firefox trust Windows OS certificates, set `security.enterprise_roots.enabled` to `true` in `about:config`, or import the certificate directly in Firefox Settings under *Privacy & Security* &rarr; *Certificates* &rarr; *View Certificates* &rarr; *Authorities* &rarr; *Import...*.
