using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Playnite.SDK;

namespace GameUpdateStatus
{
    public abstract class Checker
    {
        protected readonly ILogger logger;

        public Checker(ILogger logger)
        {
            this.logger = logger;
        }

        public abstract List<StatusEntry> Check();

        protected abstract string FindLauncherPath();


        protected abstract StatusEntry GetLocalInfo(string manifest);
        protected abstract string GetPublicBuildId(string key);

        protected StatusEntry CheckSingle(string manifest)
        {
            StatusEntry status = GetLocalInfo(manifest);
            status.PublicBuild = GetPublicBuildId(status.AppId);

            if (string.IsNullOrWhiteSpace(status.PublicBuild))
            {
                status.Status = UpdateStatus.Unknown.ToString();

                logger.Info("  Public : ?");
                logger.Info("  Etat   : Inconnu");
            }
            else if (status.LocalBuild == status.PublicBuild)
            {
                status.Status = UpdateStatus.UpToDate.ToString();

                logger.Info("  Public : " + status.PublicBuild);
                logger.Info("  Etat   : À jour");
            }
            else
            {
                status.Status = UpdateStatus.UpdateAvailable.ToString();

                logger.Info("  Public : " + status.PublicBuild);
                logger.Info("  Etat   : Mise à jour disponible");
            }

            return status;
        }

    }
}