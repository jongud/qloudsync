﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;

namespace GreenQloud.UI.Setup
{
    public partial class Ready : Form
    {
        public Ready()
        {
            InitializeComponent();
        }



        private void button1_Click(object sender, EventArgs e)
        {
            Program.Controller.OpenStorageQloudWebSite();
            Program.Controller.OpenStorageFolder();
            this.Hide();
            this.Close();
        }

        private void button1_KeyDown(object sender, KeyEventArgs e)
        {
            button1_Click(sender, e);
        }

        private void pathlabel_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {
      
        }


    }
}
