using SchrodingerServer.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace SchrodingerServer.Permissions;

public class SchrodingerServerPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(SchrodingerServerPermissions.GroupName);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(SchrodingerServerPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<SchrodingerServerResource>(name);
    }
}
