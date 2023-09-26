using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Concurrent;

namespace VirtualRubik
{
    public partial class Form2 : Form
    {
        public Form2(string time, string scramble, string moves = "")
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Time info";

            label1.Text = time;
            textBox1.Text = scramble;
            textBoxRecon.Text = moves;
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void buttonRecon_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Yes;       // maybe there's a better way? Yes = reconstruct.
            this.Close();
        }
    }
}
