using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Concurrent;

namespace VirtualRubik
{
    public partial class Form1 : Form
    {
        private RubikManager rubikManager;
        private Stack<LayerMove> moveStack = new Stack<LayerMove>();
        private ConcurrentQueue<MultiLayerMove> moveQueueMulti = new ConcurrentQueue<MultiLayerMove>();            // I'm not sure why they use a stack and not a queue to store the move sequence.
        private Stack<MultiLayerMove> moveStackMulti = new Stack<MultiLayerMove>();
        private int rotationTicks = 2;
        private int rotationTicksRecon = 6;
        private System.Windows.Forms.Timer timer;
        //private Point3D rotationAccum;
        bool isScrambled = false;
        bool inputEnabled = true;
        private RubikManager.PositionSpec oldSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
        private RubikManager.PositionSpec currentSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };

        // Timer/ scramble generator components  -> Put this in a class you idiot
        string[] basicMoves = { "F", "R", "L", "B", "D", "U" };
        string[] modifiers = { "", "'", "2" };
        Random random = new Random();
        int scrambleLength = 20;
        string scramble;
        int timerState = 0;  // 0 = waiting, 1 = ready, 2 = timer running
        DateTime startTime;
        TimeSpan lastTimeSpan = TimeSpan.Zero;
        List<TimeSpan> history = new List<TimeSpan>();
        List<string> scrambleHistory = new List<string>();
       
        int timerModeState = 1;
        string[] timerModes = { "Virtual (untimed)", "Virtual (timed)", "Timer" };
        int historyIndex = 0;
        bool dnfFlag = false;

        // Recon components  - put this in a class you idiot
        string reconMoves = "";                                                         // stores the raw moves 
        ConcurrentQueue<TimeSpan> reconIntervals = new ConcurrentQueue<TimeSpan>();     // stores the time delay for each move?        
        List<string> moveHistory = new List<string>();                                  // list of raw strings
        List<ConcurrentQueue<TimeSpan>> intervalHistory = new List<ConcurrentQueue<TimeSpan>>();
        int reconMoveIndex = 0;                                                         // index of current move in reconstruction
        int reconMoveTarget = 0;                                                        // target end index - could be larger or smaller then current move index 
        //int reconStringIndex = 0;                                                     // bruh look at this dude
        string[] reconSplit;                                                            // split up into individual moves
        System.Windows.Forms.Timer timer2 = new System.Windows.Forms.Timer();
        bool reverseRecon = false;
        bool dirChange = false;

        // accelerometer instance
        accelerometer accel = null;
        bool useAccel = false;

        Cube3D.RubikPosition[] NA = { Cube3D.RubikPosition.None, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] Rw = { Cube3D.RubikPosition.RightSlice, Cube3D.RubikPosition.MiddleSlice_Sides, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] Lw = { Cube3D.RubikPosition.LeftSlice, Cube3D.RubikPosition.MiddleSlice_Sides, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] Fw = { Cube3D.RubikPosition.FrontSlice, Cube3D.RubikPosition.MiddleSlice, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] UDparallel = { Cube3D.RubikPosition.TopLayer, Cube3D.RubikPosition.BottomLayer, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] xRotationLayers = { Cube3D.RubikPosition.RightSlice, Cube3D.RubikPosition.MiddleSlice_Sides, Cube3D.RubikPosition.LeftSlice };
        Cube3D.RubikPosition[] yRotationLayers = { Cube3D.RubikPosition.BottomLayer, Cube3D.RubikPosition.MiddleLayer, Cube3D.RubikPosition.TopLayer };
        Cube3D.RubikPosition[] zRotationLayers = { Cube3D.RubikPosition.BackSlice, Cube3D.RubikPosition.MiddleSlice, Cube3D.RubikPosition.FrontSlice };
        Cube3D.RubikPosition[] R = { Cube3D.RubikPosition.RightSlice, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] L = { Cube3D.RubikPosition.LeftSlice, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] U = { Cube3D.RubikPosition.TopLayer, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] F = { Cube3D.RubikPosition.FrontSlice, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] D = { Cube3D.RubikPosition.BottomLayer, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] B = { Cube3D.RubikPosition.BackSlice, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] S = { Cube3D.RubikPosition.MiddleSlice, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        Cube3D.RubikPosition[] M = { Cube3D.RubikPosition.MiddleSlice_Sides, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
        bool[] dirCW = { true, true, true };
        bool[] dirCCW = { false, true, true };

        string testT = "R  U  R' U' R' F  R  R  U' R' U' R  U  R' F' ";
        string testRot = "x x' y y' z z'";
        string testWide = "r r'";
        string testBasic = "U U' L L' D D' B B'";

        public Form1()
        {
            // Timer specific handlers
            this.KeyUp += new KeyEventHandler(onKeyUp);
            this.KeyDown += new KeyEventHandler(onKeyDown);
            
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.UserPaint, true);
            Icon = VirtualRubik.Properties.Resources.cube;
            textBoxAnimationSteps.Text = rotationTicks.ToString();
            foreach (string name in Enum.GetNames(typeof(Cube3D.RubikPosition)))
            {
                int value = (int)Enum.Parse(typeof(Cube3D.RubikPosition), name);
                if (value != 0) comboBox1.Items.Add(name);
            }
            foreach (ToolStripMenuItem menuItem in menuStrip1.Items) ((ToolStripDropDownMenu)menuItem.DropDown).ShowImageMargin = false;
            comboBox1.SelectedIndex = 0;
            ResetCube();
            buttonUpdateStep.Text = "Change";
            textBoxAnimationSteps.Enabled = false;

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1;
            timer.Tick += timer_Tick;
            timer.Start();
            hideVirtualControl();

            generateScramble();
            updateTimesList();
            resetTimer();
            timer1.Interval = 1;
            comboBoxTimerMode.SelectedIndex = timerModeState;
            comboBoxTimerMode.DropDownStyle = ComboBoxStyle.DropDownList;   // should prevent typing into this dropdown
            comboBoxTimerMode.Enabled = false;

            tableLayoutPanelRecon.Visible = false;
            panel5.Visible = false;

            trackBar1.Maximum = 40;
            trackBar1.Value = 10;
            trackBar1.Enabled = true;
            trackBar1.Visible = false;
            trackBarReconIndex.Visible = false;
            timer2.Interval = 10;
            timer2.Tick += timer2_Tick;

            loadTestHistory();
            richTextBoxReconMoves.AddContextMenu();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            groupBox1.Width = Math.Max(Math.Min((int)((double)this.ClientRectangle.Width * 0.3), 300), 220);
            this.Invalidate();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            this.Invalidate();
            if (useAccel)
            {
                accel.processByte();
                setRotationFromAccel();
            }

            if(!rubikManager.Rotating)
            {
                MultiLayerMove nextMove;
                if(moveQueueMulti.TryDequeue(out nextMove))
                {
                    if(!reconActive)
                        rubikManager.Rotate90Multi(nextMove.Layer, nextMove.Direction, rotationTicks);  // for normal moves
                    
                    else
                    {
                        if (!reverseRecon)        
                        {
                            richTextBoxReconMoves.SelectionStart = (reconMoveIndex) * 3;
                            richTextBoxReconMoves.SelectionLength = 3;
                            richTextBoxReconMoves.SelectionBackColor = Color.MediumSeaGreen;

                            rubikManager.Rotate90Multi(nextMove.Layer, nextMove.Direction, rotationTicksRecon); // for scrambling/recon
                            richTextBoxReconMoves.Select();
                            if(reconMoveIndex<reconSplit.Length)reconMoveIndex++;
                        }
                        else
                        {
                            richTextBoxReconMoves.SelectionStart = (reconMoveIndex-1) * 3;
                            richTextBoxReconMoves.SelectionLength = 3;
                            //richTextBoxReconMoves.SelectionBackColor = Color.MediumSeaGreen;

                            richTextBoxReconMoves.SelectionBackColor = richTextBoxReconMoves.BackColor;
                            richTextBoxReconMoves.SelectionStart += 3;
                            richTextBoxReconMoves.SelectionLength = richTextBoxReconMoves.Text.Length - richTextBoxReconMoves.SelectionStart;
                            richTextBoxReconMoves.DeselectAll();

                            rubikManager.Rotate90Multi(nextMove.Layer, nextMove.Direction, rotationTicksRecon); // for scrambling/recon
                            richTextBoxReconMoves.Select();
                            if(reconMoveIndex>0)reconMoveIndex--;
                        }
                        Console.WriteLine($"Recon move index: {reconMoveIndex}"); 
                    }
                }
            }
        }

        private void loadTestHistory()
        {
            //should first reset;
            moveHistory.Add(testT);
            scrambleHistory.Add(testT);
            history.Add(TimeSpan.MinValue);
            updateTimesList();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Rectangle r = new Rectangle(0, 0, this.ClientRectangle.Width + groupBox2.Width-((groupBox1.Visible)? groupBox1.Width:0), this.ClientRectangle.Height - ((panel5.Visible)? panel5.Height:0) - ((statusStrip1.Visible)? statusStrip1.Height:0) - ((statusStrip1.Visible)? statusStrip2.Height:0) + menuStrip1.Height - panelTimerElements.Height + panelScramble.Height);
            int min = Math.Min(r.Height, r.Width);
            double factor = 3 * ((double)min / (double)400);
            if (r.Width > r.Height) r.X = (r.Width - r.Height) / 2;
            else if (r.Height > r.Width) r.Y = (r.Height - r.Width) / 2;
            RubikManager.PositionSpec selectedPos = rubikManager.Render(e.Graphics, r, factor, PointToClient(Cursor.Position));
            rubikManager.setFaceSelection(Face3D.SelectionMode.None);
            rubikManager.setFaceSelection(oldSelection.cubePos, oldSelection.facePos, Face3D.SelectionMode.Second);
            rubikManager.setFaceSelection(selectedPos.cubePos, selectedPos.facePos, Face3D.SelectionMode.First);
            currentSelection = selectedPos;
            toolStripStatusLabel2.Text = "[" + selectedPos.cubePos.ToString() + "] | " + selectedPos.facePos.ToString();
        }
        //private void dpp(Point3D p, Graphics g, Brush c)
        //{
        //    Point3D proj = p.Project(400, 400, 100, 4, 3);
        //    int size = (int)((double)3 * (3 - (proj.Z - 1.5)));
        //    g.FillEllipse(c, new Rectangle((int)proj.X-(size/2), (int)proj.Y-(size/2), size, size));
        //}

        private void scrambleCube()
        {
            groupBox1.Enabled = false;
            Random rnd = new Random();
            for (int i = 0; i < 50; i++) rubikManager.Rotate90Sync((Cube3D.RubikPosition)Math.Pow(2, rnd.Next(0, 9)), Convert.ToBoolean(rnd.Next(0, 2)));
            groupBox1.Enabled = true;
            isScrambled = true;
        }

        #region Buttons

        private void button1_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            moveStack.Clear();
        }
        private void button2_Click(object sender, EventArgs e)
        {
            groupBox1.Enabled = false;
            rotationTicks = 5;
            Cube3D.RubikPosition layer = (Cube3D.RubikPosition)Enum.Parse(typeof(Cube3D.RubikPosition), comboBox1.Text);
            bool direction = (button5.Text == "CW");
            rubikManager.Rotate90(layer, direction, rotationTicks);
            toolStripStatusLabel1.Text = "Rotating " + layer.ToString() + " " + ((button5.Text == "CW") ? "Clockwise" : "Counter-Clockwise");
        }
        private void button3_Click(object sender, EventArgs e)
        {
            String dir = "Clockwise";
            if (button5.Text != "CW") dir = "Counter-Clockwise";
            listBox1.Items.Add(comboBox1.Text + " " + dir);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count > 0)
            {
                //rotationTicks = 5;
                groupBox1.Enabled = false;
                List<LayerMove> lms = new List<LayerMove>();
                for (int i = listBox1.Items.Count - 1; i >= 0; i--)
                {
                    String[] commands = listBox1.Items[i].ToString().Split(new Char[] { Convert.ToChar(" ") });
                    lms.Add(new LayerMove((Cube3D.RubikPosition)Enum.Parse(typeof(Cube3D.RubikPosition), commands[0]), (commands[1] == "Clockwise")));
                }
                moveStack = new Stack<LayerMove>(lms);
                LayerMove nextMove = moveStack.Pop();
                bool direction = nextMove.Direction;
                if (nextMove.Layer == Cube3D.RubikPosition.TopLayer || nextMove.Layer == Cube3D.RubikPosition.LeftSlice || nextMove.Layer == Cube3D.RubikPosition.FrontSlice) direction = !direction;
                rubikManager.Rotate90(nextMove.Layer, direction, rotationTicksRecon);
                toolStripStatusLabel1.Text = "Rotating " + nextMove.Layer.ToString() + " " + ((nextMove.Direction) ? "Clockwise" : "Counter-Clockwise");
                listBox1.SelectedIndex = 0;
                comboBox1.Text = listBox1.SelectedItem.ToString();
            }
        }

        private void resetRecon()
        {
            reconMoves = "";
            reconMoveIndex = 0;
            reconMoveTarget = 0;
            moveStack = new Stack<LayerMove>();
            moveStackMulti = new Stack<MultiLayerMove>();
            moveQueueMulti = new ConcurrentQueue<MultiLayerMove>();
            reconIntervals = new ConcurrentQueue<TimeSpan>();
        }
        private void ResetCube()
        {
            resetRecon();
            rubikManager = new RubikManager();
            rubikManager.OnRotatingFinished += new RubikManager.RotatingFinishedHandler(RotatingFinished);          // i get it. Every single rotation finished will check for another move in the stack, fed from the listbox.
            //rotationAccum = new Point3D(Math.Sqrt(0.5), Math.Sqrt(0.5), Math.Sqrt(0.5));
            toolStripStatusLabel1.Text = "Ready";

            isScrambled = false;
            setCameraAngle(40);
        }

        private void scrambleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //scrambleCube();
            scrambleCubeFromString(scramble);
        }
        private void resetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetCube();
        }
        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("VirtualRubik Version " + Application.ProductVersion, "VirtualRubik - About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion


        // Every time a rotation finishes this will fire and check for more moves pushed to the stack.
        // The stack is inexorably linked to the listbox in the UI. 
        // I can add a second stack for automation, and leave this stack for manual purposes only.

        private void RotatingFinished(object sender)
        {
            if (moveStack.Count > 0)
            {
                LayerMove nextMove = moveStack.Pop();
                bool direction = nextMove.Direction;
                //if (nextMove.Layer == Cube3D.RubikPosition.TopLayer || nextMove.Layer == Cube3D.RubikPosition.LeftSlice || nextMove.Layer == Cube3D.RubikPosition.FrontSlice) direction = !direction;     // i'm pretty sure this breaks my scramble.
                rubikManager.Rotate90(nextMove.Layer, direction, rotationTicksRecon);
                toolStripStatusLabel1.Text = "Rotating " + nextMove.Layer.ToString() + " " + ((nextMove.Direction) ? "Clockwise" : "Counter-Clockwise");

                if (listBox1.Items.Count > 0)
                {
                    listBox1.SelectedIndex++;
                    comboBox1.Text = listBox1.SelectedItem.ToString();
                }
            }
            if (moveStackMulti.Count > 0)
            {
                MultiLayerMove nextMove = moveStackMulti.Pop();
                bool[] direction = nextMove.Direction;
                //if (nextMove.Layer == Cube3D.RubikPosition.TopLayer || nextMove.Layer == Cube3D.RubikPosition.LeftSlice || nextMove.Layer == Cube3D.RubikPosition.FrontSlice) direction = !direction;     // i'm pretty sure this breaks my scramble.
                rubikManager.Rotate90Multi(nextMove.Layer, nextMove.Direction, rotationTicksRecon);

            }

/*            if (moveQueueMulti.Count>0)
            {
                MultiLayerMove nextMove;
                moveQueueMulti.TryDequeue(out nextMove);
                bool[] direction = nextMove.Direction;
                //if (nextMove.Layer == Cube3D.RubikPosition.TopLayer || nextMove.Layer == Cube3D.RubikPosition.LeftSlice || nextMove.Layer == Cube3D.RubikPosition.FrontSlice) direction = !direction;     // i'm pretty sure this breaks my scramble.
                rubikManager.Rotate90Multi(nextMove.Layer, nextMove.Direction, rotationTicksForScrambling);
            }*/

            if(reconActive)
            {

            }

            else
            {
                groupBox1.Enabled = true;
                listBox1.SelectedIndex = -1;
                toolStripStatusLabel1.Text = "Ready";
            }
        }

        #region Mouse Handling

        private Point oldMousePos = new Point(-1, -1);
        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
            if (oldMousePos.X != -1 && oldMousePos.Y != -1)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right || e.Button == System.Windows.Forms.MouseButtons.Middle)
                {
                    this.Cursor = Cursors.SizeAll;
                    int dX = e.X - oldMousePos.X;
                    int dY = e.Y - oldMousePos.Y;
                    rubikManager.RubikCube.Rotation[1] -= dX / 3;
                    rubikManager.RubikCube.Rotation[0] += (dY / 3);

                    //rotationAccum.Rotate(Point3D.RotationType.X, (dY));
                    //rotationAccum.Rotate(Point3D.RotationType.Y, -(dX));
                    //double rotY = Math.Atan2(rotationAccum.X, rotationAccum.Z);
                    //Point3D pp = new Point3D(rotationAccum.X, rotationAccum.Y, rotationAccum.Z);
                    //pp.Rotate(Point3D.RotationType.Y, -rotY * (180 / Math.PI));
                    //double rotX = Math.Atan2(-pp.Y, pp.Z);
                    //pp.Rotate(Point3D.RotationType.X, -rotX * (180 / Math.PI));
                    //rubikManager.RubikCube.Rotation[0] = rotX * (180 / Math.PI);
                    //rubikManager.RubikCube.Rotation[1] = rotY * (180 / Math.PI);
                }
                else
                {
                    this.Cursor = Cursors.Arrow;
                }
            }
            oldMousePos = e.Location;

        }
        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (oldSelection.cubePos == Cube3D.RubikPosition.None || oldSelection.facePos == Face3D.FacePosition.None)
                {
                    if (currentSelection.cubePos == Cube3D.RubikPosition.None || currentSelection.facePos == Face3D.FacePosition.None)
                    {
                        rubikManager.RubikCube.cubes.ForEach(c => c.Faces.ToList().ForEach(f => f.Selection = Face3D.SelectionMode.None));
                        oldSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
                        currentSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
                    }
                    else
                    {
                        if (!Cube3D.isCorner(currentSelection.cubePos))
                        {
                            oldSelection = currentSelection;
                            rubikManager.RubikCube.cubes.ForEach(c => c.Faces.ToList().ForEach(f =>
                            {
                                if (currentSelection.cubePos != c.Position && !Cube3D.isCenter(c.Position) && currentSelection.facePos == f.Position)
                                {
                                    Cube3D.RubikPosition assocLayer = Face3D.layerAssocToFace(currentSelection.facePos);
                                    Cube3D.RubikPosition commonLayer = Cube3D.getCommonLayer(currentSelection.cubePos, c.Position, assocLayer);
                                    if (commonLayer != Cube3D.RubikPosition.None && c.Position.HasFlag(commonLayer))
                                    {
                                        f.Selection |= Face3D.SelectionMode.Possible;
                                    }
                                    else
                                    {
                                        f.Selection |= Face3D.SelectionMode.NotPossible;
                                    }
                                }
                                else
                                {
                                    f.Selection |= Face3D.SelectionMode.NotPossible;
                                }
                            }));
                            toolStripStatusLabel1.Text = "First selection: [" + currentSelection.cubePos.ToString() + "] | " + currentSelection.facePos.ToString(); ;
                        }
                        else
                        {
                            rubikManager.RubikCube.cubes.ForEach(c => c.Faces.ToList().ForEach(f => f.Selection = Face3D.SelectionMode.None));
                            toolStripStatusLabel1.Text = "Error: Invalid first selection, must not be a corner";
                        }
                    }
                }
                else
                {
                    if (currentSelection.cubePos == Cube3D.RubikPosition.None || currentSelection.facePos == Face3D.FacePosition.None)
                    {
                        toolStripStatusLabel1.Text = "Ready";
                    }
                    else
                    {
                        if (currentSelection.cubePos != oldSelection.cubePos)
                        {
                            if (!Cube3D.isCenter(currentSelection.cubePos))
                            {
                                if (oldSelection.facePos == currentSelection.facePos)
                                {
                                    Cube3D.RubikPosition assocLayer = Face3D.layerAssocToFace(oldSelection.facePos);
                                    Cube3D.RubikPosition commonLayer = Cube3D.getCommonLayer(oldSelection.cubePos, currentSelection.cubePos, assocLayer);
                                    Boolean direction = true;
                                    if (commonLayer == Cube3D.RubikPosition.TopLayer || commonLayer == Cube3D.RubikPosition.MiddleLayer || commonLayer == Cube3D.RubikPosition.BottomLayer)
                                    {
                                      if (((oldSelection.facePos == Face3D.FacePosition.Back) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.RightSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Left) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.BackSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Front) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.LeftSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Right) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.FrontSlice))) direction = false;
                                    }
                                    if (commonLayer == Cube3D.RubikPosition.LeftSlice || commonLayer == Cube3D.RubikPosition.MiddleSlice_Sides || commonLayer == Cube3D.RubikPosition.RightSlice)
                                    {
                                      if (((oldSelection.facePos == Face3D.FacePosition.Bottom) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.BackSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Back) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.TopLayer))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Top) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.FrontSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Front) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.BottomLayer))) direction = false;
                                    }
                                    if (commonLayer == Cube3D.RubikPosition.BackSlice || commonLayer == Cube3D.RubikPosition.MiddleSlice || commonLayer == Cube3D.RubikPosition.FrontSlice)
                                    {
                                      if (((oldSelection.facePos == Face3D.FacePosition.Top) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.RightSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Right) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.BottomLayer))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Bottom) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.LeftSlice))
                                      || ((oldSelection.facePos == Face3D.FacePosition.Left) && currentSelection.cubePos.HasFlag(Cube3D.RubikPosition.TopLayer))) direction = false;
                                    }
                                    if (commonLayer != Cube3D.RubikPosition.None)
                                    {
                                        if (groupBox1.Enabled)
                                        {
                                            int temp = rotationTicks;
                                            groupBox1.Enabled = false;
                                            rotationTicks = 25;
                                            if (commonLayer == Cube3D.RubikPosition.TopLayer || commonLayer == Cube3D.RubikPosition.LeftSlice || commonLayer == Cube3D.RubikPosition.FrontSlice) direction = !direction;
                                            rubikManager.Rotate90(commonLayer, direction, rotationTicks);
                                            comboBox1.Text = commonLayer.ToString();
                                            toolStripStatusLabel1.Text = "Rotating " + commonLayer.ToString() + " " + ((direction) ? "Clockwise" : "Counter-Clockwise");
                                            rotationTicks = temp;
                                        }
                                    }
                                    else
                                    {
                                        toolStripStatusLabel1.Text = "Error: Invalid second selection, does not specify distinct layer";
                                    }
                                }
                                else
                                {
                                    toolStripStatusLabel1.Text = "Error: Invalid second selection, must match orientation of first selection";
                                }
                            }
                            else
                            {
                                toolStripStatusLabel1.Text = "Error: Invalid second selection, must not be a center";
                            }
                        }
                        else
                        {
                            toolStripStatusLabel1.Text = "Error: Invalid second selection, must not be first selection";
                        }
                    }
                    rubikManager.RubikCube.cubes.ForEach(c => c.Faces.ToList().ForEach(f => f.Selection = Face3D.SelectionMode.None));
                    oldSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
                    currentSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
                }
            }
        }
        private void groupBox1_EnabledChanged(object sender, EventArgs e)
        {
            if (!groupBox1.Enabled)
            {
                rubikManager.RubikCube.cubes.ForEach(c => c.Faces.ToList().ForEach(f => f.Selection = Face3D.SelectionMode.None));
                oldSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
                currentSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
            }
        }

        #endregion

        private void hideVirtualControl()
        {
            toolStripMenuItem1.Text = "Controls <<";
            statusStrip1.Visible = false;
            statusStrip2.Visible = false;
            groupBox1.Visible = false;
        }
        private void showVirtualControl()
        {
            toolStripMenuItem1.Text = "Controls >>";
            statusStrip1.Visible = true;
            statusStrip2.Visible = true;
            groupBox1.Visible = true;
        }
        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (toolStripMenuItem1.Text == "Controls <<")
            {
                toolStripMenuItem1.Text = "Controls >>";
                statusStrip1.Visible = true;
                statusStrip2.Visible = true;
                groupBox1.Visible = true;
            }
            else
            {
                toolStripMenuItem1.Text = "Controls <<";
                statusStrip1.Visible = false;
                statusStrip2.Visible = false;
                groupBox1.Visible = false;
            }
        }


        #region New event handlers/ helpers

        private void setCameraAngle(int angleY, int angleX = 0)
        {
            rubikManager.RubikCube.Rotation[0] = angleY;
        }
        private void setRotationFromAccel()
        {
            float factor = 5F;
            rubikManager.RubikCube.Rotation[0] = factor*(accel.getY() - 127);
            rubikManager.RubikCube.Rotation[1] = factor*(accel.getX() - 127);
        }

        bool[] invertDir(bool[] dir)
        {
            bool[] newdir = new bool[3];
            dir.CopyTo(newdir,0);
            for(int i = 0; i < 3; i++)
            {
                newdir[i] = !dir[i];
            }
            return newdir;
        }

        private MultiLayerMove processSingleMove(string input, int steps = 6)
        {
            rotationTicksRecon = steps;
            IDictionary<string, Cube3D.RubikPosition[]> rubikPositions = new Dictionary<string, Cube3D.RubikPosition[]>();
            rubikPositions.Add("R", R);
            rubikPositions.Add("U", U);
            rubikPositions.Add("D", D);
            rubikPositions.Add("L", L);
            rubikPositions.Add("F", F);
            rubikPositions.Add("B", B);
            rubikPositions.Add("S", S);
            rubikPositions.Add("M", M);
            rubikPositions.Add("", NA);

            rubikPositions.Add("r", Rw);
            rubikPositions.Add("l", Lw);
            rubikPositions.Add("f", Fw);

            rubikPositions.Add("x", xRotationLayers);
            rubikPositions.Add("y", yRotationLayers);
            rubikPositions.Add("z", zRotationLayers);

            IDictionary<string, bool[]> specialDirections = new Dictionary<string, bool[]>();
            specialDirections.Add("x", new bool[] { true, true, false });
            specialDirections.Add("y", new bool[] { false, false, true });
            specialDirections.Add("z", new bool[] { true, true, false });
            specialDirections.Add("l", new bool[] { true, false, true });
            specialDirections.Add("f", new bool[] { true, false, true });
            specialDirections.Add("M", dirCCW);
            
            char[] toTrim = { '\'', ' '};

            if (!input.Equals(""))
            {
                bool[] directionIn = dirCW;
                Cube3D.RubikPosition[] position = NA;
                string cleaned = input.TrimEnd(toTrim);
                if (specialDirections.ContainsKey(cleaned))
                {
                    directionIn = specialDirections[cleaned];
                }
                if (input.EndsWith(@"'"))
                {
                    directionIn = invertDir(directionIn);
                }
                try
                {
                    position = rubikPositions[cleaned];
                    MultiLayerMove move = new MultiLayerMove(position, directionIn);
                    //moveQueueMulti.Enqueue(move);
                    return move;
                }
                catch
                {

                }
            }
            return new MultiLayerMove(NA, dirCW);
        }

        // including all the messed up wide moves and stuff.
        private void executeSolutionFromString(string input, int steps = 6)
        {
            moveStack = new Stack<LayerMove>();
            moveStackMulti = new Stack<MultiLayerMove>();
            moveQueueMulti = new ConcurrentQueue<MultiLayerMove>();
            rotationTicksRecon = steps;

/*            IDictionary<string, Cube3D.RubikPosition[]> rubikPositions = new Dictionary<string, Cube3D.RubikPosition[]>();
            rubikPositions.Add("R", R);
            rubikPositions.Add("U", U);
            rubikPositions.Add("D", D);
            rubikPositions.Add("L", L);
            rubikPositions.Add("F", F);
            rubikPositions.Add("B", B);
            rubikPositions.Add("S", S);
            rubikPositions.Add("M", M);
            rubikPositions.Add("", NA);

            rubikPositions.Add("r", Rw);
            rubikPositions.Add("l", Lw);
            rubikPositions.Add("f", Fw);

            rubikPositions.Add("x", xRotationLayers);
            rubikPositions.Add("y", yRotationLayers);
            rubikPositions.Add("z", zRotationLayers);

            IDictionary<string, bool[]> specialDirections = new Dictionary<string, bool[]>();
            specialDirections.Add("x", new bool[] { true, true, false });
            specialDirections.Add("y", new bool[] { false, false, true });
            specialDirections.Add("z", new bool[] { true, true, false });
            specialDirections.Add("l", new bool[] { true, false, true });
            specialDirections.Add("f", new bool[] { true, false, true });
            specialDirections.Add("M", dirCCW);*/

            string[] split = input.Split(' '); 
            //char[] toTrim = { '\'',};

            List<MultiLayerMove> moveList = new List<MultiLayerMove>();
            
            foreach (string s in split)
            {
               
                if (!s.Equals(""))
                {
                    moveList.Add(processSingleMove(s, steps));
                   /* bool[] directionIn = dirCW;
                    Cube3D.RubikPosition[] position = NA;
                    string cleaned = s.TrimEnd(toTrim);
                    if (specialDirections.ContainsKey(cleaned))
                    {
                        directionIn = specialDirections[cleaned];
                    }
                    if (s.EndsWith(@"'"))
                    {
                        directionIn = invertDir(directionIn);
                    }
                    try
                    {
                        position = rubikPositions[cleaned];

                        //moveQueueMulti.Enqueue(new MultiLayerMove(position, directionIn));
                        moveList.Add(new MultiLayerMove(position, directionIn));
                    }
                    catch
                    {

                    }*/
                }

            }
            moveQueueMulti = new ConcurrentQueue<MultiLayerMove>(moveList);
/*          MultiLayerMove nextMove;
            moveQueueMulti.TryDequeue(out nextMove);
            bool[] direction = nextMove.Direction;
            //if (nextMove.Layer == Cube3D.RubikPosition.TopLayer || nextMove.Layer == Cube3D.RubikPosition.LeftSlice || nextMove.Layer == Cube3D.RubikPosition.FrontSlice) direction = !direction;     // i'm pretty sure this breaks my scramble.
            rubikManager.Rotate90Multi(nextMove.Layer, nextMove.Direction, rotationTicksForScrambling);*/
        }

        // only normal moves
        private void executeMovesFromString(string input, int steps = 2)
        {
            moveStack = new Stack<LayerMove>();
            moveStackMulti = new Stack<MultiLayerMove>();

            //rotationTicksRecon = steps;      
            
            IDictionary<string, Cube3D.RubikPosition> rubikPositions = new Dictionary<string, Cube3D.RubikPosition>();
            rubikPositions.Add("R", Cube3D.RubikPosition.RightSlice);
            rubikPositions.Add("L", Cube3D.RubikPosition.LeftSlice);
            rubikPositions.Add("U", Cube3D.RubikPosition.TopLayer);
            rubikPositions.Add("D", Cube3D.RubikPosition.BottomLayer);
            rubikPositions.Add("F", Cube3D.RubikPosition.FrontSlice);
            rubikPositions.Add("B", Cube3D.RubikPosition.BackSlice);
            rubikPositions.Add("", Cube3D.RubikPosition.None);

            string[] split = input.Split(' '); char[] toTrim = { '\'', ' ', '2' };
            //split.Reverse();                // to use the stack, reverse the string. To use Rotate90Sync, don't reverse. But this could be avoided if we just used a queue for the moves, right?
            List<LayerMove> lms = new List<LayerMove>();            
            foreach (string s in split)                   
            {
                bool directionIn = true;
                int times = 1;
                Cube3D.RubikPosition position = Cube3D.RubikPosition.None;
                if (s.EndsWith(@"'"))
                {
                    directionIn = false;
                }
                if (s.EndsWith(@"2"))
                {
                    times = 2;
                }
                string cleaned = s.TrimEnd(toTrim);
                try
                {
                    position = rubikPositions[cleaned];
                    for (int i = 1; i <= times; i++)
                    {
                        rubikManager.Rotate90Sync(position, directionIn);               
                        //lms.Add(new LayerMove(position, directionIn));
                    }
                }
                catch
                {

                }
            }
/*            moveStack = new Stack<LayerMove>(lms);
            LayerMove nextMove = moveStack.Pop();
            bool direction = nextMove.Direction;
            rubikManager.Rotate90(nextMove.Layer, direction, rotationTicksForScrambling);*/
            isScrambled = true;
        }

        private void scrambleCubeFromString(string input, int steps=0)
        {
            ResetCube();
            executeMovesFromString(input,steps);
        }
        private void button5_Click(object sender, EventArgs e)
        {
            if (button5.Text == "CW")
            {
                button5.Text = "CCW";
            }
            else
            {
                button5.Text = "CW";
            }
        }

        private void setInputEnable()
        {

            if (comboBoxTimerMode.SelectedIndex == 0 || comboBoxTimerMode.SelectedIndex == 1)
            {
                inputEnabled = true;
            }
            else
            {
                inputEnabled = false;
            }

        }

/*      // implement this solver later.
        private void solveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CubeSolver cs = new CubeSolver(rubikManager);
            cs.Solve();
        }
*/
        private void buttonUpdateStep_MouseClick(object sender, MouseEventArgs e)
        {
            if(buttonUpdateStep.Text.Equals( "Change"))
            {
                buttonUpdateStep.Text = "Set";
                textBoxAnimationSteps.Enabled = true;
                inputEnabled = false;
            }
            else
            {
                buttonUpdateStep.Text = "Change";
                textBoxAnimationSteps.Enabled = false;
                inputEnabled = true;
                try
                {
                    rotationTicks = Convert.ToInt32(textBoxAnimationSteps.Text);
                }
                catch { }
                finally
                {
                    textBoxAnimationSteps.Text = rotationTicks.ToString();
                }
                
            }
            
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            timerModeState = comboBoxTimerMode.SelectedIndex;
            setInputEnable();
        }

        private void readyTimer()
        {
            timerState = 1;
            startTime = DateTime.Now;
            textBoxMainTimer.BackColor = Color.Green;
        }
        private void startTimer()
        {
            timerState = 2;
            startTime = DateTime.Now;
            timer1.Start();
            labelCurrentScramble.Visible = false;
        }
        private void stopTimer()
        {
            timerState = 0;
            timer1.Stop();
            updateTimer();
            textBoxMainTimer.BackColor = Color.White;

            scrambleHistory.Add(scramble);
            if(!dnfFlag || timerModeState == 2)
            {
                history.Add(lastTimeSpan);
                moveHistory.Add(reconMoves);
            }
            else
            {
                history.Add(TimeSpan.MaxValue);
                moveHistory.Add(""); // empty queue
            } 
            
            labelCurrentScramble.Visible = true;
            updateTimesList();
            generateScramble();
            resetRecon();
        }
        private void onKeyDown(object sender, KeyEventArgs e)
        {
            if(timerModeState == 2)
            {
                switch (timerState)
                {
                    case 0:
                        if (e.KeyCode == Keys.Escape)
                        {
                            resetTimer();
                        }
                        if (e.KeyCode == Keys.Space)
                        {
                            readyTimer();
                        }
                        break;
                    case 1:
                        break;
                    case 2:
                        if (e.KeyCode == Keys.Space)
                        {
                            stopTimer();
                        }
                        break;
                }
            }
        }
        private void onKeyUp(object sender, KeyEventArgs e)
        {
            if (timerModeState == 2)
            {
                switch (timerState)
                {
                    case 0:
                        break;
                    case 1:
                        if (e.KeyCode == Keys.Space)
                        {
                            startTimer();
                        }
                        break;
                    case 2:
                        break;
                }
            }
        }


        // Key binding. 
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            /* 
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Delete)
            {
                rubikManager.RubikCube.cubes.ForEach(c => c.Faces.ToList().ForEach(f => f.Selection = Face3D.SelectionMode.None));
                oldSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
                currentSelection = new RubikManager.PositionSpec() { cubePos = Cube3D.RubikPosition.None, facePos = Face3D.FacePosition.None };
            }
            */
            Cube3D.RubikPosition[] layers = { Cube3D.RubikPosition.None, Cube3D.RubikPosition.None, Cube3D.RubikPosition.None };
            bool[] dir = { true, true, true };

            string move = "";

            if (inputEnabled)
            {
                bool isLegalinInspection = false;
                switch (e.KeyCode) 
                {
                    case Keys.Space:
                        if(!isScrambled && !reconActive) scrambleCubeFromString(scramble); isLegalinInspection = true;
                        break;
                    case Keys.Escape:
                        if(timerState ==2) dnfFlag = true;
                        resetTimer(); ResetCube(); 
                        isLegalinInspection = true;
                        break;
                    #region Rotations
                    case Keys.Back:
                    case Keys.OemQuotes:
                        //rubikManager.CubeRotate90("x", true, step);
                        layers = xRotationLayers; dir = new bool[] { true, true, false };isLegalinInspection = true;
                        move = "x ";
                        break;
                    case Keys.Z:
                        //rubikManager.CubeRotate90("x", false, step);
                        layers = xRotationLayers;dir = new bool[] { false, false, true };isLegalinInspection = true;
                        move = "x'";
                        break;
                    case Keys.OemSemicolon:
                        //rubikManager.CubeRotate90("z", true, step);
                        layers = zRotationLayers;
                        dir = new bool[] { true, true, false };isLegalinInspection = true;
                        move = "z ";
                        break;
                    case Keys.Q:
                        //rubikManager.CubeRotate90("z", false, step);
                        layers = zRotationLayers;dir = new bool[] { false, false, true}; isLegalinInspection = true;
                        move = "z'";
                        break;
                    case Keys.A:
                        //rubikManager.CubeRotate90("y", true, step);
                        layers = yRotationLayers;dir = new bool[] { true, true, false };isLegalinInspection = true;
                        move = "y'";
                        break;
                    case Keys.O:
                        //rubikManager.CubeRotate90("y", false, step);
                        layers = yRotationLayers; dir = new bool[] { false, false, true };isLegalinInspection = true;
                        move = "y ";
                        break;
                    #endregion 

                    #region wide moves
                    // wide moves
                    case Keys.L:
                        //rubikManager.Rotate90Multi(Rw, new bool[] { true, true, true }, step); 
                        layers = Rw; dir = new bool[] { true, true, true }; move = "r ";
                        break;
                    case Keys.M:
                        //rubikManager.Rotate90Multi(Rw, new bool[] { false, false, true }, step); 
                        layers = Rw;dir = new bool[] { false, false, true };move = "r'";
                        break;
                    case Keys.G:
                        //rubikManager.Rotate90Multi(Lw, new bool[] { false, true, true }, step); 
                        layers = Lw;dir = new bool[] { false, true, true };move = "l'";
                        break;
                    case Keys.C:
                        //rubikManager.Rotate90Multi(Lw, new bool[] { true, false, true }, step); 
                        layers = Lw;dir = new bool[] { true, false, true }; move = "l ";
                        break;
                    case Keys.ShiftKey:
                        //rubikManager.Rotate90Multi(Fw, new bool[] { false, true, true }, step); 
                        layers = Fw; dir = new bool[] { false, true, true }; move = "f'";
                        break;
                    case Keys.OemQuestion:
                        //rubikManager.Rotate90Multi(Fw, new bool[] { true, false, true }, step); 
                        layers = Fw;dir = new bool[] { true, false, true };move = "f ";
                        break;
                    #endregion 

                    #region normal moves
                    // normal moves
                    case Keys.D5:
                    case Keys.D6:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.MiddleSlice_Sides, false, step);
                        layers = M;dir = dirCCW;move = "M ";break;
                    case Keys.W:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.BackSlice, true, step);
                        layers = B;dir = dirCW;move = "B ";break;
                    case Keys.F:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.LeftSlice, false, step);
                        layers = L;dir = dirCCW;move = "L'";break;
                    case Keys.P:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.MiddleSlice, true, step); 
                        layers = S;dir = dirCW;move = "S ";break;
                    case Keys.J:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.MiddleSlice, false, step);
                        layers = S;dir = dirCCW;move = "S'";break;
                    case Keys.U:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.RightSlice, true, step); 
                        layers = R;dir = dirCW;move = "R ";break;
                    case Keys.Y:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.BackSlice, false, step);
                        layers = B;dir = dirCCW;move = "B'";break;
                    case Keys.R:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.BottomLayer, true, step); break;
                        layers = D; dir = dirCW; move = "D "; break;
                    case Keys.S:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.LeftSlice, true, step); break;
                        layers = L; dir = dirCW; move = "L ";  break;
                    case Keys.T:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.TopLayer, false, step); break;
                        layers = U; dir = dirCCW; move = "U'"; break;
                    case Keys.B:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.FrontSlice, false, step); break;
                        layers = F; dir = dirCCW; move = "F'";  break;
                    case Keys.K:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.FrontSlice, true, step); break;
                        layers = F; dir = dirCW; move = "F "; break;
                    case Keys.N:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.TopLayer, true, step); break;
                        layers = U; dir = dirCW; move = "U ";  break;
                    case Keys.E:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.RightSlice, false, step); break;
                        layers = R; dir = dirCCW; move = "R'"; break;
                    case Keys.I:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.BottomLayer, false, step); break;
                        layers = D; dir = dirCCW; move = "D'"; break;
                    case Keys.V:
                    case Keys.Oemcomma:
                        //rubikManager.Rotate90(Cube3D.RubikPosition.MiddleSlice_Sides, true,step); break;
                        layers = M; dir = dirCW; move = "M'"; break;

                    # endregion
                    default:
                        break;
                }

                MultiLayerMove moveMulti = new MultiLayerMove(layers, dir);
                moveQueueMulti.Enqueue(moveMulti);
                Console.WriteLine(moveMulti.Layer[0]);
                if (timerModeState == 1 && isScrambled)      // if virtual timed and scrambled
                {
                    reconMoves += move + " ";
                    //reconIntervals.Enqueue(lastTimeSpan);     // work on the timing later
                }

                if (timerModeState == 1 && timerState == 0 && !isLegalinInspection && isScrambled)  // if virtual timed and timer idle
                {
                    startTimer();
                    dnfFlag = false;
                }
            }


            else  // recon keybinds
            {
                e.SuppressKeyPress = true;
                switch(e.KeyCode)
                {
                    case Keys.K:
                        toggleReconPlayback();
                        break;
                    case Keys.Escape:
                        resetReconAni();
                        break;
                    default:
                        break;
                }
            }
        }


        private TimeSpan AverageOfN(List<TimeSpan> list, int N)
        {
            List<TimeSpan> subList = new List<TimeSpan>();
            if (list.Count >= N)
            {
                for (int index = (list.Count - 1); index > list.Count - N - 1; index--)
                {
                    subList.Add(list[index]);
                }
                subList.Remove(subList.Min());          // AoN is the mean of the set with best and worst times removed
                subList.Remove(subList.Max());
                if(subList.Contains(TimeSpan.MaxValue))
                { 
                    return TimeSpan.MaxValue; 
                }
                else
                {
                    return (subList.Mean());
                }
                
            }
            else
            {
                return TimeSpan.MaxValue;           
            }
        }
        private void updateTimesList()
        {
            listBoxTimesList.Items.Clear();
            foreach (TimeSpan timeSpan in history)
            {
                listBoxTimesList.Items.Add(timeSpanToString(timeSpan));
            }
            listBoxTimesList.TopIndex = listBoxTimesList.Items.Count - 1;
            TimeSpan ao5 = AverageOfN(history, 5);
            textBoxAo5.Text = timeSpanToString(ao5);
            TimeSpan ao12 = AverageOfN(history, 12);
            textBoxAo12.Text = timeSpanToString(ao12);
        }

        private bool isSolved()
        {
            bool solved = true;
            Color bottomColor = rubikManager.getFaceColor(Cube3D.RubikPosition.BottomLayer | Cube3D.RubikPosition.MiddleSlice_Sides | Cube3D.RubikPosition.MiddleSlice, Face3D.FacePosition.Bottom);
            Color topColor = rubikManager.getFaceColor(Cube3D.RubikPosition.TopLayer | Cube3D.RubikPosition.MiddleSlice_Sides | Cube3D.RubikPosition.MiddleSlice, Face3D.FacePosition.Top);
            Color frontColor = rubikManager.getFaceColor(Cube3D.RubikPosition.FrontSlice | Cube3D.RubikPosition.MiddleSlice_Sides | Cube3D.RubikPosition.MiddleLayer, Face3D.FacePosition.Front);
            Color rightColor = rubikManager.getFaceColor(Cube3D.RubikPosition.RightSlice | Cube3D.RubikPosition.MiddleSlice | Cube3D.RubikPosition.MiddleLayer, Face3D.FacePosition.Right);
            Color leftColor = rubikManager.getFaceColor(Cube3D.RubikPosition.LeftSlice | Cube3D.RubikPosition.MiddleSlice | Cube3D.RubikPosition.MiddleLayer, Face3D.FacePosition.Left);
            Color backColor = rubikManager.getFaceColor(Cube3D.RubikPosition.BackSlice | Cube3D.RubikPosition.MiddleSlice_Sides | Cube3D.RubikPosition.MiddleLayer, Face3D.FacePosition.Back);
            /*IEnumerable<Cube3D> bottomEdges = rubikManager.RubikCube.cubes.Where(c => Cube3D.isEdge(Cube3D.RubikPosition.BottomLayer));
            IEnumerable<Cube3D> bottomCorners = rubikManager.RubikCube.cubes.Where(c => Cube3D.isEdge(Cube3D.RubikPosition.BottomLayer));*/

            foreach (Cube3D c in rubikManager.RubikCube.cubes)
            {
                if (c.Position.HasFlag(Cube3D.RubikPosition.BottomLayer) && c.Faces.First(f => f.Position == Face3D.FacePosition.Bottom).Color != bottomColor)
                {
                    return false;
                }
                else if (c.Position.HasFlag(Cube3D.RubikPosition.TopLayer) && c.Faces.First(f => f.Position == Face3D.FacePosition.Top).Color != topColor)
                {
                    return false;
                }
                else if (c.Position.HasFlag(Cube3D.RubikPosition.FrontSlice) && c.Faces.First(f => f.Position == Face3D.FacePosition.Front).Color != frontColor)
                {
                    return false;
                }
                else if (c.Position.HasFlag(Cube3D.RubikPosition.RightSlice) && c.Faces.First(f => f.Position == Face3D.FacePosition.Right).Color != rightColor)
                {
                    return false;
                }
                else if (c.Position.HasFlag(Cube3D.RubikPosition.LeftSlice) && c.Faces.First(f => f.Position == Face3D.FacePosition.Left).Color != leftColor)
                {
                    return false;
                }
                // No need to check last side.
                else
                {
                }
            }
            if(solved)
            {
                isScrambled = false;
            }
            return solved;
        }

        private string timeSpanToString(System.TimeSpan time)
        {
            if (time.Equals(TimeSpan.MaxValue))
            {
                return " - ";           // surely this could never cause a weird impossible to find bug. never.
            }
            else
            {
                return $"{time:mm}:{time:ss}.{time:fff}";
            }
        }

        private void updateTimer()
        {
            lastTimeSpan = DateTime.Now - startTime;
            textBoxMainTimer.Text = timeSpanToString((DateTime.Now - startTime));
        }

        private void resetTimer()
        {
            lastTimeSpan = TimeSpan.Zero;
            textBoxMainTimer.Text = timeSpanToString(lastTimeSpan);
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            updateTimer();
            Console.WriteLine($"Solved: {isSolved().ToString()}");
            if (timerModeState == 1 && timerState == 2 && isSolved())       // bad, don't do this here. You should do this on key input, but that had some timing issue. 
            {
                stopTimer();
            }
        }

        private void textBoxMainTimer_Enter(object sender, EventArgs e)
        {
            textBoxMainTimer.Enabled = false;
            textBoxMainTimer.Enabled = true;
        }

        private void buttonResetHist_MouseClick(object sender, MouseEventArgs e)
        {
            history.Clear();
            scrambleHistory.Clear();
            moveHistory.Clear();
            intervalHistory.Clear();
            updateTimesList();
        }

        private void generateScramble()
        {
            string output = "";
            int lastMoveIndex = 0;
            for (int i = 0; i < scrambleLength; i++)
            {
                int moveIndex = random.Next(basicMoves.Length);
                while (moveIndex == lastMoveIndex)
                {
                    moveIndex = random.Next(basicMoves.Length); // re-roll until it isn't a duplicate
                }
                lastMoveIndex = moveIndex;
                int modifierIndex = random.Next(modifiers.Length);
                output += $"{basicMoves[moveIndex]}{modifiers[modifierIndex]} ";
            }
            labelCurrentScramble.Text = output;
            scramble = output;
        }

        private void buttonNextScramble_MouseClick(object sender, MouseEventArgs e)
        {
            generateScramble();
        }

        private void buttonSetTimerMode_MouseClick(object sender, MouseEventArgs e)
        {
            if(buttonSetTimerMode.Text.Equals("Change mode"))
            {
                buttonSetTimerMode.Text = "Set mode";
                comboBoxTimerMode.Enabled = true;
            }
            else
            {
                buttonSetTimerMode.Text = "Change mode";
                comboBoxTimerMode.Enabled = false;
            }
        }

        private void accelerometerToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            accel.close();
            closeToolStripMenuItem.Visible = false;
            openToolStripMenuItem.Visible = true;
            useAccel = false;
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string portName = "COM8"; // not dynamic. too bad, we're using this until I figure out the toolstrip.
            accel = new accelerometer(portName);
            closeToolStripMenuItem.Visible = true;
            openToolStripMenuItem.Visible = false;
            useAccel = true;
        }


        bool reconActive = false;
        private void listBoxTimesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            historyIndex = listBoxTimesList.SelectedIndex;
        }

        int lockedHistoryIndex = 0;
        private void listBoxTimesList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if(historyIndex!=-1 && history.Count > 0 && !reconActive)
            {
                ResetCube();
                reconActive = true;
                lockedHistoryIndex = historyIndex;
                Form2 dialog = new Form2(timeSpanToString(history[lockedHistoryIndex]), scrambleHistory[lockedHistoryIndex], moveHistory[lockedHistoryIndex]);
                DialogResult result = dialog.ShowDialog();
                if(result == DialogResult.OK)
                {
                    history.RemoveAt(lockedHistoryIndex);
                    scrambleHistory.RemoveAt(lockedHistoryIndex);
                    moveHistory.RemoveAt(lockedHistoryIndex);
                    reconActive = false;
                }
                else if(result == DialogResult.Yes)
                {
                    tableLayoutPanelRecon.Visible = true;
                    panel5.Visible = true;
                    trackBar1.Visible = true;
                    trackBarReconIndex.Visible = true;
                    inputEnabled = false;
                    reconActive = true;
                    buttonReconPlay.Text = "Play";

                    reconMoveIndex = 0;
                 
                    textBoxMainTimer.Text =  timeSpanToString( history[lockedHistoryIndex] );
                    richTextBoxReconMoves.Text = moveHistory[lockedHistoryIndex];
                    textBoxReconScramble.Text = scrambleHistory[lockedHistoryIndex];
                    scrambleCubeFromString(scrambleHistory[lockedHistoryIndex]);

                    reconSplit = removeEmptyMoves(moveHistory[lockedHistoryIndex].Split(' '));
                    reconMoveTarget = 0;
                    reverseRecon = false;

                    trackBarReconIndex.Maximum = reconSplit.Length;
                    trackBarReconIndex.Value = reconMoveTarget;
                }
                else
                {
                    reconActive = false;
                    inputEnabled = true;
                }
                updateTimesList();
            }
            
        }

        private string[] removeEmptyMoves(string[] input)
        {
            List<string> output = new List<string>(); int outIndex = 0;
            for(int i = 0; i < input.Length; i++)
            {
                if(!input[i].Equals(""))
                {
                    output.Add(input[i]);
                    outIndex++;
                }
            }
            string[] result = output.ToArray();
            return result;
        }

        private void buttonCloseRecon_Click(object sender, EventArgs e)
        { 
            tableLayoutPanelRecon.Visible = false;
            panel5.Visible = false;
            trackBar1.Visible = false;
            trackBarReconIndex.Visible = false;
            richTextBoxReconMoves.SelectAll();
            richTextBoxReconMoves.SelectionBackColor = richTextBoxReconMoves.BackColor;

            ResetCube(); resetTimer(); resetRecon();  
            inputEnabled = true; reconActive = false;
            moveStackMulti = new Stack<MultiLayerMove>();
            moveStack = new Stack<LayerMove>();
            moveQueueMulti = new ConcurrentQueue<MultiLayerMove>();

            reconMoveIndex = 0;
            reconMoveTarget = 0;
            timer2.Enabled = false;
        }

        // this works for the timed playback. It's really iffy for manual scrubbing.
        private bool loadNextReconMove()
        {
            if (reconActive && ((moveQueueMulti.Count == 0 && 0 <= reconMoveIndex && reconMoveIndex < reconSplit.Length)))     // ensures we only add one recon move at a time to the queue, which the other timer works on.
            {
                if(dirChange)
                {
                    dirChange = false;
                }
                MultiLayerMove move = processSingleMove(reconSplit[reconMoveIndex], trackBar1.Value);
                moveQueueMulti.Enqueue(move);

                Console.WriteLine($"Processed index {reconMoveIndex}, reversed = {reverseRecon}");
                return true;
            }
            return false;
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (loadNextReconMove() && 0 <= reconMoveTarget && reconMoveTarget < reconSplit.Length)     // ensures we only add one recon move at a time to the queue, which the other timer works on.
            {
                //trackBarReconIndex.Value = reconMoveTarget;
                if (reconMoveTarget < reconSplit.Length) reconMoveTarget++;                                 // timer only shuffles along the target, it's up to main timer to update and keep up    
            }
            trackBarReconIndex.Value = reconMoveIndex;
        }

        private void resetReconAni()
        {
            moveQueueMulti = new ConcurrentQueue<MultiLayerMove>();
            moveStack = new Stack<LayerMove>();
            moveStackMulti = new Stack<MultiLayerMove>();
            reconMoveIndex = 0;
            reconMoveTarget = 0;
            trackBarReconIndex.Value = reconMoveTarget;

            richTextBoxReconMoves.SelectAll();
            richTextBoxReconMoves.SelectionBackColor = richTextBoxReconMoves.BackColor;
            richTextBoxReconMoves.DeselectAll();
            buttonReconPlay.Text = "Play";
            scrambleCubeFromString(scrambleHistory[lockedHistoryIndex]);

            trackBarReconIndex.Enabled = true;
            timer2.Enabled = false;
        }
        private void buttonResetReconAni_Click(object sender, EventArgs e)
        {
            resetReconAni();
        }

        private void toggleReconPlayback()
        {
            if (buttonReconPlay.Text == "Play")
            {
                reverseRecon = false;
                buttonReconPlay.Text = "Pause";
                trackBarReconIndex.Enabled = false;
                timer2.Enabled = true;
            }
            else
            {
                reverseRecon = false;
                timer2.Enabled = false;
                trackBarReconIndex.Enabled = true;
                buttonReconPlay.Text = "Play";
            }
        }

        private void buttonReconPlay_MouseClick(object sender, MouseEventArgs e)
        {
            toggleReconPlayback();
        }

        private void trackBarReconIndex_ValueChanged(object sender, EventArgs e)
        {
            if (buttonReconPlay.Text == "Play" && reconActive)
            {
                bool lastDir = reverseRecon;
                int lastTarget = reconMoveTarget;
                reconMoveTarget = trackBarReconIndex.Value;
                if (lastTarget > reconMoveTarget)
                {
                    reverseRecon = true;
                }
                else if (lastTarget < reconMoveTarget) 
                {
                    reverseRecon = false;
                }
                Console.WriteLine($"Last target: {lastTarget}       Target: {reconMoveTarget}       Reverse: {reverseRecon}");
                // append a sequence of moves from lastTarget to newTarget into the moveQueueMulti
                if (!reverseRecon)
                {
                    for (int i = lastTarget; i < reconMoveTarget; i++)
                    {
                        moveQueueMulti.Enqueue(processSingleMove(reconSplit[i], trackBar1.Value));
                        Console.WriteLine($"Enqueued move index {i}");
                    }
                }
                else
                {
                    for (int j = lastTarget; j > reconMoveTarget; j--)
                    {
                        MultiLayerMove move = processSingleMove(reconSplit[j - 1], trackBar1.Value);
                        MultiLayerMove reverseMove = new MultiLayerMove(move.Layer, invertDir(move.Direction));
                        moveQueueMulti.Enqueue(reverseMove);
                        Console.WriteLine($"Enqueued reverse move for index {j}");
                    }
                }
            }
        }


        int oldRotationTicksRecon = 10;
        private void trackBarReconIndex_MouseDown(object sender, MouseEventArgs e)
        {
            oldRotationTicksRecon = rotationTicksRecon;
            trackBar1.Value = 0;
        }

        private void trackBarReconIndex_MouseUp(object sender, MouseEventArgs e)
        {
/*            if (buttonReconPlay.Text == "Play" && reconActive)
            {
                bool lastDir = reverseRecon;
                int lastTarget = reconMoveTarget;
                reconMoveTarget = trackBarReconIndex.Value;
                if (lastTarget >= reconMoveTarget)
                {
                    reverseRecon = true;
                }
                else
                {
                    reverseRecon = false;
                }
                Console.WriteLine($"Last target: {lastTarget}       Target: {reconMoveTarget}       Reverse: {reverseRecon}");
                // append a sequence of moves from lastTarget to newTarget into the moveQueueMulti
                //List<MultiLayerMove> moves = new List<MultiLayerMove>();
                if (!reverseRecon)
                {
                    for (int i = lastTarget; i < reconMoveTarget; i++)
                    {
                        //moves.Add(processSingleMove(reconSplit[i], rotationTicksRecon));
                        moveQueueMulti.Enqueue(processSingleMove(reconSplit[i], trackBar1.Value));
                        Console.WriteLine($"Enqueued move index {i}");
                    }
                }
                else
                {
                    for (int j = lastTarget; j > reconMoveTarget; j--)
                    {
                        MultiLayerMove move = processSingleMove(reconSplit[j - 1], trackBar1.Value);
                        MultiLayerMove reverseMove = new MultiLayerMove(move.Layer, invertDir(move.Direction));
                        //moves.Add(reverseMove);
                        moveQueueMulti.Enqueue(reverseMove);
                        Console.WriteLine($"Enqueued reverse move for index {j}");
                    }
                }
            }*/
            trackBar1.Value = oldRotationTicksRecon;
        }

        private void trackBarReconIndex_MouseEnter(object sender, EventArgs e)
        {
        }

        private void trackBarReconIndex_Scroll(object sender, EventArgs e)
        {
            
        }
    }

    #endregion
   
    public class ExRichTextBox : RichTextBox
    {
        public ExRichTextBox()
        {
            Selectable = true;
        }
        const int WM_SETFOCUS = 0x0007;
        const int WM_KILLFOCUS = 0x0008;

        ///<summary>
        /// Enables or disables selection highlight. 
        /// If you set `Selectable` to `false` then the selection highlight
        /// will be disabled. 
        /// It's enabled by default.
        ///</summary>
        [DefaultValue(true)]
        public bool Selectable { get; set; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SETFOCUS && !Selectable)
                m.Msg = WM_KILLFOCUS;

            base.WndProc(ref m);
        }
    }
    public static class Extension
    {
        public static void AddContextMenu(this RichTextBox rtb)
        {
            if (rtb.ContextMenuStrip == null)
            {
                ContextMenuStrip cms = new ContextMenuStrip()
                {
                    ShowImageMargin = false
                };

                ToolStripMenuItem tsmiUndo = new ToolStripMenuItem("Undo");
                tsmiUndo.Click += (sender, e) => rtb.Undo();
                cms.Items.Add(tsmiUndo);

                ToolStripMenuItem tsmiRedo = new ToolStripMenuItem("Redo");
                tsmiRedo.Click += (sender, e) => rtb.Redo();
                cms.Items.Add(tsmiRedo);

                cms.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem tsmiCut = new ToolStripMenuItem("Cut");
                tsmiCut.Click += (sender, e) => rtb.Cut();
                cms.Items.Add(tsmiCut);

                ToolStripMenuItem tsmiCopy = new ToolStripMenuItem("Copy");
                tsmiCopy.Click += (sender, e) => rtb.Copy();
                cms.Items.Add(tsmiCopy);

                ToolStripMenuItem tsmiPaste = new ToolStripMenuItem("Paste");
                tsmiPaste.Click += (sender, e) => rtb.Paste();
                cms.Items.Add(tsmiPaste);

                ToolStripMenuItem tsmiDelete = new ToolStripMenuItem("Delete");
                tsmiDelete.Click += (sender, e) => rtb.SelectedText = "";
                cms.Items.Add(tsmiDelete);

                cms.Items.Add(new ToolStripSeparator());

                ToolStripMenuItem tsmiSelectAll = new ToolStripMenuItem("Select All");
                tsmiSelectAll.Click += (sender, e) => rtb.SelectAll();
                cms.Items.Add(tsmiSelectAll);

                cms.Opening += (sender, e) =>
                {
                    tsmiUndo.Enabled = !rtb.ReadOnly && rtb.CanUndo;
                    tsmiRedo.Enabled = !rtb.ReadOnly && rtb.CanRedo;
                    tsmiCut.Enabled = !rtb.ReadOnly && rtb.SelectionLength > 0;
                    tsmiCopy.Enabled = rtb.SelectionLength > 0;
                    tsmiPaste.Enabled = !rtb.ReadOnly && Clipboard.ContainsText();
                    tsmiDelete.Enabled = !rtb.ReadOnly && rtb.SelectionLength > 0;
                    tsmiSelectAll.Enabled = rtb.TextLength > 0 && rtb.SelectionLength < rtb.TextLength;
                };

                rtb.ContextMenuStrip = cms;
            }
        }
        public static TimeSpan Mean(this ICollection<TimeSpan> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            long mean = 0L;
            long remainder = 0L;
            int n = source.Count;
            foreach (var item in source)
            {
                long ticks = item.Ticks;
                mean += ticks / n;
                remainder += ticks % n;
                mean += remainder / n;
                remainder %= n;
            }
            return TimeSpan.FromTicks(mean);
        }
    }
}
