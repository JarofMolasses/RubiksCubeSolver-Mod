using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VirtualRubik
{
    class MultiLayerMove
    {
        public Cube3D.RubikPosition[] Layer;
        public bool[] Direction;

        public MultiLayerMove(Cube3D.RubikPosition[] layer_, bool[] direction_)
        {
            Layer = layer_;
            Direction = direction_;
        }
    }
}
