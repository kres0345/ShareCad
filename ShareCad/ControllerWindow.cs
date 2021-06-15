﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareCad
{
    public partial class ControllerWindow : Form
    {
        private NetworkRole networkRole;

        public event Action<NetworkRole> OnActivateShareFunctionality;

        public ControllerWindow()
        {
            InitializeComponent();
        }

        public enum NetworkRole
        {
            Guest,
            Host
        }

        private void ControllerWindow_Load(object sender, EventArgs e)
        {

        }

        private void ApplyRadioButtonStyle(RadioButton radioButton)
        {
            if (radioButton.Checked)
            {
                radioButton.BackColor = Color.FromArgb(192, 255, 192);
            }
            else
            {
                radioButton.BackColor = SystemColors.Control;
            }
        }

        private void RadioButton_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton radioButton = (RadioButton)sender;

            ApplyRadioButtonStyle(radioButton);

            if (!radioButton.Checked)
            {
                return;
            }

            switch (radioButton.Text)
            {
                case "Vært":
                    networkRole = NetworkRole.Host;
                    break;
                case "Gæst":
                    networkRole = NetworkRole.Guest;
                    break;
                default:
                    break;
            }
        }

        private void buttonActivateNetworking_Click(object sender, EventArgs e)
        {
            radioButtonGuest.Enabled = false;
            radioButtonHost.Enabled = false;

            OnActivateShareFunctionality?.Invoke(networkRole);
        }

        private void ControlSharecadForm_Load(object sender, EventArgs e)
        {

        }
    }
}
