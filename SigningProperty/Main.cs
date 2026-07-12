using SharpShell.Attributes;
using SharpShell.SharpPropertySheet;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SigningProperty
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFiles)]
    public class SigningProperty : SharpPropertySheet
    {
        protected override bool CanShowSheet()
        {
            //  We will only show the resources pages if we have ONE file selected.
            if (SelectedItemPaths.Count() != 1) return false;
            foreach (var a in SelectedItemPaths)
            {
                var fileExtension = Path.GetExtension(a);
                switch (fileExtension)
                {
                    case ".exe":
                    case ".msi":
                    case ".appx":
                    case ".msix":
                    case ".dll":
                    case ".rdp":
                    case ".pdf":
                    case ".vba":
                    case ".cat":
                    case ".msm":
                    case ".msp":
                    case ".appxbundle":
                    case ".sys":
                    case ".ocx":
                    case ".com":
                        return true;
                    default:
                        return false;
                }
            }
            return false;
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
