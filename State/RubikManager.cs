using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;

namespace VirtualRubik
{
    class RubikManager
    {
        public int moves = 0;
        public Rubik RubikCube;
        public Boolean Rotating;            
        private double rotationStep;

        private double[] rotationStepArr = { 0, 0, 0 };
        private Cube3D.RubikPosition rotationLayer;

        private Cube3D.RubikPosition[] rotationLayerArr = { Cube3D.RubikPosition.None, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        private int rotationTarget;

        private int[] rotationTargetArr = { 0, 0, 0 };
        public delegate void RotatingFinishedHandler(object sender);
        public event RotatingFinishedHandler OnRotatingFinished;

        private void BroadcastRotatingFinished()
        {
            if (OnRotatingFinished == null) return;
            OnRotatingFinished(this);
        }

        public struct PositionSpec
        {
            public Cube3D.RubikPosition cubePos;
            public Face3D.FacePosition facePos;
        }

        public RubikManager(Color cfront, Color cback, Color ctop, Color cbottom, Color cright, Color cleft)
        {
            RubikCube = new Rubik();
            setFaceColor(Cube3D.RubikPosition.FrontSlice, Face3D.FacePosition.Front, cfront);
            setFaceColor(Cube3D.RubikPosition.BackSlice, Face3D.FacePosition.Back, cback);
            setFaceColor(Cube3D.RubikPosition.TopLayer, Face3D.FacePosition.Top, ctop);
            setFaceColor(Cube3D.RubikPosition.BottomLayer, Face3D.FacePosition.Bottom, cbottom);
            setFaceColor(Cube3D.RubikPosition.RightSlice, Face3D.FacePosition.Right, cright);
            setFaceColor(Cube3D.RubikPosition.LeftSlice, Face3D.FacePosition.Left, cleft);
            Rotating = false;
        }
        public RubikManager() : this(Color.Green, Color.RoyalBlue, Color.White, Color.Yellow, Color.Red, Color.Orange) { }

/*        public void CubeRotate90(string input, bool direction, int step)
        {
            bool activeDirection = direction;
            switch (input.ToLower())
            {
                case "x":
                    Cube3D.RubikPosition[] xRotationLayers = { Cube3D.RubikPosition.RightSlice, Cube3D.RubikPosition.MiddleSlice_Sides, Cube3D.RubikPosition.LeftSlice };
                    bool[] xRotationDirs = { direction, direction, !direction };
                    Rotate90Multi(xRotationLayers, xRotationDirs, step);
                    break;
                case "y":
                    Cube3D.RubikPosition[] yRotationLayers = { Cube3D.RubikPosition.BottomLayer, Cube3D.RubikPosition.MiddleLayer, Cube3D.RubikPosition.TopLayer };
                    bool[] yRotationDirs = { direction, direction, !direction };
                    Rotate90Multi(yRotationLayers, yRotationDirs, step);
                    break;
                case "z":
                    Cube3D.RubikPosition[] zRotationLayers = { Cube3D.RubikPosition.BackSlice, Cube3D.RubikPosition.MiddleSlice, Cube3D.RubikPosition.FrontSlice };
                    bool[] zRotationDirs = { direction, direction, !direction };
                    Rotate90Multi(zRotationLayers, zRotationDirs, step);
                    break;
                default:
                    break;
            }
        }*/

        public void clearStateArrays()
        {
            for (int i = 0; i < 3; i++)
            {
                rotationLayerArr[i] = Cube3D.RubikPosition.None;
                rotationStepArr[i] = 0;
                rotationTargetArr[i] = 0;
            }
        }
        // only animates a single layer turn
        public void Rotate90(Cube3D.RubikPosition layer, bool direction, int steps)
        {
            if (!Rotating)
            {
                clearStateArrays();
                Rotating = true;
                rotationLayerArr[0] = layer;
                rotationStepArr[0] = (double)90 / (double)steps;
                if (layer == Cube3D.RubikPosition.TopLayer || layer == Cube3D.RubikPosition.LeftSlice || layer == Cube3D.RubikPosition.FrontSlice) direction = !direction;
                if (direction) rotationStepArr[0] *= (-1);
                rotationTargetArr[0] = 90;
                if (direction) rotationTargetArr[0] = -90;
            }
        }


        public void Rotate90Multi(Cube3D.RubikPosition[] layer, bool[] direction, int steps)
        {
            if (!Rotating)
            {
                clearStateArrays();
                Rotating = true;

                    for (int l = 0; l < 3; l++)
                    {
                        bool directiontemp = direction[l];
                        rotationLayerArr[l] = layer[l];
                        rotationStepArr[l] = (double)90 / (double)steps;
                        if (layer[l] == Cube3D.RubikPosition.TopLayer || layer[l] == Cube3D.RubikPosition.LeftSlice || layer[l] == Cube3D.RubikPosition.FrontSlice) directiontemp = !direction[l];     // why does this break it ????????
                        if (directiontemp) rotationStepArr[l] *= (-1);
                        rotationTargetArr[l] = 90;
                        if (directiontemp) rotationTargetArr[l] = -90;
                    }

            }
        }
        

    public void Rotate90Sync(Cube3D.RubikPosition layer, bool direction)
    {
      if (!Rotating)
      {
        Rotate90(layer, direction, 1);
        RubikCube.LayerRotation[layer] += rotationStep;
        resetFlags(false);
      }
      moves++;
    }

/*    public void RotateNTimesSync(Cube3D.RubikPosition layer, bool direction, int n = 1)
    {
        for(int i = 0; i < n; i++)
        {
            Rotate90Sync(layer, direction);
        }
    }
*/
    public void setFaceColor(Cube3D.RubikPosition affected, Face3D.FacePosition face, Color color)
    {
      RubikCube.cubes.Where(c => c.Position.HasFlag(affected)).ToList().ForEach(c => c.Faces.Where(f => f.Position == face).ToList().ForEach(f => f.Color = color));
      RubikCube.cubes.ToList().ForEach(c => { c.Colors.Clear(); c.Faces.ToList().ForEach(f => c.Colors.Add(f.Color)); });
    }

    public Color getFaceColor(Cube3D.RubikPosition position, Face3D.FacePosition face)
    {
      return RubikCube.cubes.First(c => c.Position.HasFlag(position)).Faces.First(f => f.Position == face).Color;
    }

    public void setFaceSelection(Cube3D.RubikPosition affected, Face3D.FacePosition face, Face3D.SelectionMode selection)
    {
      RubikCube.cubes.Where(c => c.Position.HasFlag(affected)).ToList().ForEach(c => c.Faces.Where(f => f.Position == face).ToList().ForEach(f =>
      {
        if (f.Selection.HasFlag(Face3D.SelectionMode.Possible))
        {
          f.Selection = selection | Face3D.SelectionMode.Possible;
        }
        else if (f.Selection.HasFlag(Face3D.SelectionMode.NotPossible))
        {
          f.Selection = selection | Face3D.SelectionMode.NotPossible;
        }
        else
        {
          f.Selection = selection;
        }
      }));
    }
    public void setFaceSelection(Face3D.SelectionMode selection)
    {
      RubikCube.cubes.ToList().ForEach(c => c.Faces.ToList().ForEach(f =>
      {
        if (f.Selection.HasFlag(Face3D.SelectionMode.Possible))
        {
          f.Selection = selection | Face3D.SelectionMode.Possible;
        }
        else if (f.Selection.HasFlag(Face3D.SelectionMode.NotPossible))
        {
          f.Selection = selection | Face3D.SelectionMode.NotPossible;
        }
        else
        {
          f.Selection = selection;
        }
      }));
    }

    public PositionSpec Render(Graphics g, Rectangle screen, double scale, Point mousePos)
    {
      PositionSpec result = RubikCube.Render(g, screen, scale, mousePos);
      for(int l = 0; l < 3; l++)
      {
              if (Rotating)
              {
                RubikCube.LayerRotation[rotationLayerArr[l]] += rotationStepArr[l];
              }      
              if ((rotationTargetArr[0] > 0 && RubikCube.LayerRotation[rotationLayerArr[0]] >= rotationTargetArr[0]) || (rotationTargetArr[0] < 0 && RubikCube.LayerRotation[rotationLayerArr[0]] <= rotationTargetArr[0]))
              {
                 resetFlags(true);
              }
      }


    return result;
    }

    public void resetFlags(bool fireFinished)
    {
      for(int l = 0; l < 3; l++)
      {
        RubikCube.LayerRotation[rotationLayerArr[l]] = rotationTargetArr[l];
        List<Cube3D> affected = RubikCube.cubes.Where(c => c.Position.HasFlag(rotationLayerArr[l])).ToList();
        if (rotationLayerArr[l] == Cube3D.RubikPosition.LeftSlice || rotationLayerArr[l] == Cube3D.RubikPosition.MiddleSlice_Sides || rotationLayerArr[l] == Cube3D.RubikPosition.RightSlice)
        {
        affected.ForEach(c => c.Faces.ToList().ForEach(f => f.Rotate(Point3D.RotationType.X, rotationTargetArr[l])));
        }
        if (rotationLayerArr[l] == Cube3D.RubikPosition.TopLayer || rotationLayerArr[l] == Cube3D.RubikPosition.MiddleLayer || rotationLayerArr[l] == Cube3D.RubikPosition.BottomLayer)
        {
        affected.ForEach(c => c.Faces.ToList().ForEach(f => f.Rotate(Point3D.RotationType.Y, rotationTargetArr[l])));
        }
        if (rotationLayerArr[l] == Cube3D.RubikPosition.BackSlice || rotationLayerArr[l] == Cube3D.RubikPosition.MiddleSlice || rotationLayerArr[l] == Cube3D.RubikPosition.FrontSlice)
        {
        affected.ForEach(c => c.Faces.ToList().ForEach(f => f.Rotate(Point3D.RotationType.Z, rotationTargetArr[l])));
        }
      }

    double ed = ((double)2 / (double)3);
    for (int i = -1; i <= 1; i++)
    {
        for (int j = -1; j <= 1; j++)
        {
            for (int k = -1; k <= 1; k++)
            {
                //Reset Flags but keep Colors
                Cube3D.RubikPosition flags = RubikCube.genSideFlags(i, j, k); ;
                Cube3D cube = RubikCube.cubes.First(c => (Math.Round(c.Faces.Sum(f => f.Edges.Sum(e => e.X)) / 24, 4) == Math.Round(i * ed, 4))
                    && (Math.Round(c.Faces.Sum(f => f.Edges.Sum(e => e.Y)) / 24, 4) == Math.Round(j * ed, 4))
                    && (Math.Round(c.Faces.Sum(f => f.Edges.Sum(e => e.Z)) / 24, 4) == Math.Round(k * ed, 4)));
                cube.Position = flags;
                cube.Faces.ToList().ForEach(f => f.MasterPosition = flags);
                cube.Faces.First(f => (Math.Round(f.Edges.Sum(e => e.X) / 4, 4) == Math.Round(i * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Y) / 4, 4) == Math.Round((j * ed) - (ed / 2), 4))
                    && (Math.Round(f.Edges.Sum(e => e.Z) / 4, 4) == Math.Round(k * ed, 4))).Position = Face3D.FacePosition.Top;
                cube.Faces.First(f => (Math.Round(f.Edges.Sum(e => e.X) / 4, 4) == Math.Round(i * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Y) / 4, 4) == Math.Round((j * ed) + (ed / 2), 4))
                    && (Math.Round(f.Edges.Sum(e => e.Z) / 4, 4) == Math.Round(k * ed, 4))).Position = Face3D.FacePosition.Bottom;
                cube.Faces.First(f => (Math.Round(f.Edges.Sum(e => e.X) / 4, 4) == Math.Round((i * ed) - (ed / 2), 4))
                    && (Math.Round(f.Edges.Sum(e => e.Y) / 4, 4) == Math.Round(j * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Z) / 4, 4) == Math.Round(k * ed, 4))).Position = Face3D.FacePosition.Left;
                cube.Faces.First(f => (Math.Round(f.Edges.Sum(e => e.X) / 4, 4) == Math.Round((i * ed) + (ed / 2), 4))
                    && (Math.Round(f.Edges.Sum(e => e.Y) / 4, 4) == Math.Round(j * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Z) / 4, 4) == Math.Round(k * ed, 4))).Position = Face3D.FacePosition.Right;
                cube.Faces.First(f => (Math.Round(f.Edges.Sum(e => e.X) / 4, 4) == Math.Round(i * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Y) / 4, 4) == Math.Round(j * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Z) / 4, 4) == Math.Round((k * ed) - (ed / 2), 4))).Position = Face3D.FacePosition.Front;
                cube.Faces.First(f => (Math.Round(f.Edges.Sum(e => e.X) / 4, 4) == Math.Round(i * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Y) / 4, 4) == Math.Round(j * ed, 4))
                    && (Math.Round(f.Edges.Sum(e => e.Z) / 4, 4) == Math.Round((k * ed) + (ed / 2), 4))).Position = Face3D.FacePosition.Back;
            }
        }
    }
    foreach (Cube3D.RubikPosition rp in (Cube3D.RubikPosition[])Enum.GetValues(typeof(Cube3D.RubikPosition))) RubikCube.LayerRotation[rp] = 0;
    Rotating = false;
      if (fireFinished) BroadcastRotatingFinished();
    }

  }
}
