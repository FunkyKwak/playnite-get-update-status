using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK; // Nécessaire pour les popups de notification/dialogue

public class LegendaryManager
{
    private readonly IDialogsFactory _dialogs;

    public LegendaryManager(IDialogsFactory dialogs)
    {
        _dialogs = dialogs;
    }

    static private string GetLegendaryPath()
    {
        // Récupère le dossier où se trouve la DLL de ton extension
        string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        return Path.Combine(path, "resources", "legendary.exe");
    }

    /// <summary>
    /// S'assure que Legendary est prêt et authentifié avant d'exécuter des requêtes.
    /// </summary>
    public async Task<bool> EnsureAuthenticatedAsync()
    {
        string legendaryPath = GetLegendaryPath();

        if (!File.Exists(legendaryPath))
        {
            _dialogs.ShowErrorMessage(
                $"Le fichier legendary.exe est introuvable dans le dossier de l'addon : {legendaryPath}", 
                "Erreur Addon Epic"
            );
            return false;
        }

        // 1. Vérification de l'état de connexion
        bool isLoggedIn = await IsLegendaryLoggedInAsync(legendaryPath);

        if (isLoggedIn)
        {
            return true;
        }

        // 2. Si non connecté, on demande confirmation à l'utilisateur
        MessageBoxResult result = _dialogs.ShowMessage(
            "Connexion à Epic Games requise pour vérifier les mises à jour.\n\nSouhaites-tu ouvrir la fenêtre d'authentification Legendary ?",
            "Authentification requise",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question
        );

        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        // 3. Lancement du processus d'authentification interactif
        AuthenticateLegendary(legendaryPath);

        // 4. Seconde vérification après la tentative de connexion
        return await IsLegendaryLoggedInAsync(legendaryPath);
    }

    private async Task<bool> IsLegendaryLoggedInAsync(string legendaryPath)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = legendaryPath,
            Arguments = "status --json",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return await Task.Run(() =>
        {
            using (var process = new System.Diagnostics.Process { StartInfo = psi })
            {
                process.Start();
                
                // Lit la sortie standard de manière synchrone dans la tâche de fond
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0 && output.Contains("\"account\"") && !output.Contains("\"<not logged in>\"");
            }
        });
    }

    private void AuthenticateLegendary(string legendaryPath)
    {
        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = legendaryPath,
            Arguments = "auth",
            UseShellExecute = true // Ouvre la console de commande visible pour saisir le jeton 'sid'
        };

        using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
        {
            process?.WaitForExit();
        }
    }

    public static async Task<string> GetLegendaryBuildIdAsync(string appId, string legendaryPath = null)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = legendaryPath ?? GetLegendaryPath(),
            Arguments = $"info {appId} --json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return await Task.Run(() =>
        {
            using (var process = new System.Diagnostics.Process { StartInfo = psi })
            {
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return null;
                }

                try
                {
                    return ExtractPublicBuildId(output);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        });
    }

    private static string ExtractPublicBuildId(string json)
    {
        // On cherche :
        // "version": "dev_n43"

        string pattern = "\"version\": \"(.*?)\"";

        Match match = Regex.Match(
            json,
            pattern,
            RegexOptions.Singleline);

        return match.Success
            ? match.Groups[1].Value
            : null;
    }
}