using SharpShell.Attributes;
using SharpShell.SharpPropertySheet;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace SigningProperty
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".exe", ".dll", ".sys", ".efi", ".scr", ".msi", ".appx", ".appxbundle", ".msix", ".msixbundle", ".cat", ".cab", ".js", ".vbs", ".wsf", ".ps1", ".xap")]
    public class SigningProperty : SharpPropertySheet
    {
        protected override bool CanShowSheet()
        {
            //  We will only show the resources pages if we have ONE file selected.
            return SelectedItemPaths.Count() == 1;
        }

        protected override IEnumerable<SharpPropertyPage> CreatePages()
        {
            //  Create the property sheet page.
            var page = new PropertyPage();

            //  Return the pages we've created.
            return new[] { page };
        }
    }
}
