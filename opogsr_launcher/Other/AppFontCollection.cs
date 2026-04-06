using Avalonia.Media.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher
{
    internal class AppFontCollection : EmbeddedFontCollection
    {
        public AppFontCollection() : base(new Uri("fonts:AppFonts", UriKind.Absolute), new Uri($"avares://{Assembly.GetExecutingAssembly().GetName().Name}/Assets/Fonts", UriKind.Absolute)) { }
    }
}
