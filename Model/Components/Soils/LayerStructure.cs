﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml;

namespace Model.Components.Soils
{
    public class LayerStructure : Model.Core.Model
    {
        public double[] Thickness { get; set; }
    }
}
