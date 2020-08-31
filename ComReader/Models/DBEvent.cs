using System;
using System.Collections.Generic;
using System.Text;

namespace ComReader.Models
{
    class DBEvent
    {
        public int Id { get; set; }
        public int Type { get; set; }
        public DBSeries Series { get; set; }
        public DateTime EventDate { get; set; }
        public DateTime CreatedDate { get; set; }

    }
}
