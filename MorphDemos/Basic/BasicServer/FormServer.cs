﻿using Basic;
using Morph.Daemon.Client;
using System.Windows.Forms;

namespace BasicServer
{
    public partial class FormServer : Form, BasicUI
    {
        public FormServer()
        {
            InitializeComponent();
            MorphManager.Startup(2);
            MorphManager.Services.StartServiceShared(BasicInterface.ServiceName, true, true, new BasicDefaultImpl(this), new BasicFactories());
        }

        private void FormServer_FormClosed(object sender, FormClosedEventArgs e)
        {
            MorphManager.Shutdown();
        }

        #region Invoke

        delegate string MethodGetValue(TextBoxBase TextBox);
        delegate void MethodSetValue(TextBoxBase TextBox, string Value);

        private string RetrieveValue(TextBoxBase TextBox)
        {
            return TextBox.Text;
        }

        private void ChangeValue(TextBoxBase TextBox, string Value)
        {
            TextBox.Text = Value;
        }

        #endregion

        #region BasicUI

        public int Number
        {
            get { return int.Parse((string)Invoke(new MethodGetValue(RetrieveValue), new object[] { edNumber })); }
            set { Invoke(new MethodSetValue(ChangeValue), new object[] { edNumber, value.ToString() }); }
        }

        public string Str
        {
            get { return (string)Invoke(new MethodGetValue(RetrieveValue), new object[] { edText }); }
            set { Invoke(new MethodSetValue(ChangeValue), new object[] { edText, value }); }
        }

        #endregion
    }
}