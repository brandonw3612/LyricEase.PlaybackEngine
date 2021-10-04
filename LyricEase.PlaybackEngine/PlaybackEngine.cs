using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyricEase.PlaybackEngine
{
    public class PlaybackEngine
    {
        private IPlayer _current;
        public IPlayer Current
        {
            get
            {
                if (_current == null)
                {
                    _current = new DoxPlayer();
                }
                return _current;
            }
        }
    }
}
