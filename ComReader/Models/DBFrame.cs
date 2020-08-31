using System;
using System.Collections.Generic;
using System.Text;

namespace ComReader.Models
{
    public class DBFrame
    {
        public int Id { get; set; }
        public int EventNumber { get; set; }
        public int Channel0 { get; set; }
        public int Channel1 { get; set; }
        public int Channel2 { get; set; }
        public int Channel3 { get; set; }
        public int Channel4 { get; set; }
        public int Channel5 { get; set; }
        public int Channel6 { get; set; }
        public int Channel7 { get; set; }
        public DBSeries Series { get; set; }
        public DateTime FrameTime { get; set; }
        public DateTime EventTime { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
