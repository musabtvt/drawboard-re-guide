using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DrawboardProSetup
{
    public class InstallerForm : Form
    {
        private Label titleLabel;
        private Label statusLabel;
        private ProgressBar progressBar;
        private Button actionButton;
        private bool installDone = false;

        public InstallerForm()
        {
            Text = "Drawboard PDF Pro (Offline) - Setup";
            Size = new Size(480, 210);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;

            titleLabel = new Label
            {
                Text = "Drawboard PDF Pro Unlimited (v6.92.3)",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 24, 27),
                Location = new Point(22, 20),
                Size = new Size(420, 28)
            };

            statusLabel = new Label
            {
                Text = "Preparing installation...",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(82, 82, 91),
                Location = new Point(24, 58),
                Size = new Size(420, 24)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(24, 92),
                Size = new Size(416, 22),
                Minimum = 0,
                Maximum = 100,
                Value = 5
            };

            actionButton = new Button
            {
                Text = "Installing...",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(340, 130),
                Size = new Size(100, 30),
                Enabled = false
            };
            actionButton.Click += (s, e) => Close();

            Controls.Add(titleLabel);
            Controls.Add(statusLabel);
            Controls.Add(progressBar);
            Controls.Add(actionButton);

            Shown += async (s, e) => await RunInstallAsync();
        }

        private void UpdateUi(string text, int progress)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateUi(text, progress)));
                return;
            }
            statusLabel.Text = text;
            progressBar.Value = Math.Min(100, Math.Max(0, progress));
        }

        private async Task RunInstallAsync()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DrawboardProSetup_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(tempDir);

                    UpdateUi("Extracting application package...", 15);
                    string zipPath = Path.Combine(tempDir, "payload.zip");
                    using (Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
                    {
                        if (res == null) throw new Exception("Embedded payload.zip resource not found.");
                        using (FileStream fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
                        {
                            res.CopyTo(fs);
                        }
                    }
                    ZipFile.ExtractToDirectory(zipPath, tempDir);
                    File.Delete(zipPath);

                    string cerPath = Path.Combine(tempDir, "DrawboardPro.cer");
                    string msixPath = Path.Combine(tempDir, "DrawboardPDF_Pro.msix");
                    string depPath = Path.Combine(tempDir, "Microsoft.UI.Xaml.2.8.appx");

                    UpdateUi("Configuring trusted signing certificate...", 35);
                    X509Certificate2 cert = new X509Certificate2(cerPath);
                    InstallCert(cert, StoreName.Root);
                    InstallCert(cert, StoreName.TrustedPeople);

                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"))
                        {
                            if (key != null) key.SetValue("AllowAllTrustedApps", 1, RegistryValueKind.DWord);
                        }
                    }
                    catch { }

                    UpdateUi("Installing Drawboard PDF Pro (please wait)...", 60);
                    string psScript = Path.Combine(tempDir, "install.ps1");
                    string scriptBody = @"
$ErrorActionPreference = 'Stop'
Get-Process -Name 'Db.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$existing = Get-AppxPackage -Name '*DRAWBOARD.DRAWBOARDPDF*' -ErrorAction SilentlyContinue
if ($existing) {
    $existing | Remove-AppxPackage -ErrorAction SilentlyContinue
}
if (Test-Path '" + depPath.Replace("'", "''") + @"') {
    try { Add-AppxPackage -Path '" + depPath.Replace("'", "''") + @"' -ErrorAction Stop } catch { }
}
Add-AppxPackage -Path '" + msixPath.Replace("'", "''") + @"' -ErrorAction Stop
$pkg = Get-AppxPackage -Name '*DRAWBOARD.DRAWBOARDPDF*'
if (-not $pkg -or $pkg.Status -ne 'Ok') {
    throw 'Package deployment verification failed.'
}
";
                    File.WriteAllText(psScript, scriptBody);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"" + psScript + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    using (Process p = Process.Start(psi))
                    {
                        string stdout = p.StandardOutput.ReadToEnd();
                        string stderr = p.StandardError.ReadToEnd();
                        p.WaitForExit();
                        if (p.ExitCode != 0)
                        {
                            string detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                            throw new Exception("Deployment failed (code " + p.ExitCode + "): " + detail.Trim());
                        }
                    }

                    UpdateUi("Installation complete! Launch Drawboard PDF from the Start Menu.", 100);
                    installDone = true;
                });

                actionButton.Text = "Finish";
                actionButton.Enabled = true;
            }
            catch (Exception ex)
            {
                statusLabel.ForeColor = Color.Firebrick;
                statusLabel.Text = "Installation failed.";
                progressBar.Value = 0;
                actionButton.Text = "Close";
                actionButton.Enabled = true;
                MessageBox.Show(this, ex.Message, "Drawboard PDF Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static void InstallCert(X509Certificate2 cert, StoreName storeName)
        {
            using (X509Store store = new X509Store(storeName, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadWrite);
                bool exists = false;
                foreach (X509Certificate2 c in store.Certificates)
                {
                    if (string.Equals(c.Thumbprint, cert.Thumbprint, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists) store.Add(cert);
                store.Close();
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }
}
