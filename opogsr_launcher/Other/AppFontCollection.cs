using Avalonia.Media.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opogsr_launcher
{
    internal class AppFontCollection : EmbeddedFontCollection
    {
        public AppFontCollection() : base(new Uri("fonts:Bender", UriKind.Absolute), new Uri("avares://Assets/Fonts", UriKind.Absolute)) { }
    }
}
