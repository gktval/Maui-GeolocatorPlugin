using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Maui.ApplicationModel.Permissions;

namespace Sample;
public static class Utils
{
    public static async Task<bool> CheckPermissions(BasePermission permission, bool requestPermission)
    {
        var permissionStatus = await permission.CheckStatusAsync();
        if (!requestPermission && permissionStatus == PermissionStatus.Denied)
        {
            var title = $"{permission} Permission";
            var question = $"To use this feature the {permission} permission is required. Please go into Settings and turn on {permission} for the app.";
            var positive = "Settings";
            var negative = "Maybe Later";
            var task = Application.Current?.MainPage?.DisplayAlert(title, question, positive, negative);
            if (task == null)
                return false;

            var result = await task;
            if (result)
            {
                AppInfo.ShowSettingsUI();
            }

            return false;
        }

        if (requestPermission || permissionStatus != PermissionStatus.Granted)
        {
            var newStatus = await permission.RequestAsync();
            if (newStatus != PermissionStatus.Granted)
            {
                var title = $"{permission} Permission";
                var question = $"To use the plugin the {permission} permission is required.";
                var positive = "Settings";
                var negative = "Maybe Later";
                var task = Application.Current?.MainPage?.DisplayAlert(title, question, positive, negative);
                if (task == null)
                    return false;

                var result = await task;
                if (result)
                {
                    AppInfo.ShowSettingsUI();
                }
                return false;
            }
        }

        return true;
    }
}