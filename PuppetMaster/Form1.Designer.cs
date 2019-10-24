namespace PuppetMaster
{
    partial class PuppetMaster
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.CommandBox = new System.Windows.Forms.TextBox();
            this.Send = new System.Windows.Forms.Button();
            this.PastCommand = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // CommandBox
            // 
            this.CommandBox.Location = new System.Drawing.Point(33, 345);
            this.CommandBox.Name = "CommandBox";
            this.CommandBox.Size = new System.Drawing.Size(726, 26);
            this.CommandBox.TabIndex = 0;
            this.CommandBox.TextChanged += new System.EventHandler(this.TextBox1_TextChanged);
            // 
            // Send
            // 
            this.Send.Location = new System.Drawing.Point(264, 377);
            this.Send.Name = "Send";
            this.Send.Size = new System.Drawing.Size(233, 51);
            this.Send.TabIndex = 1;
            this.Send.Text = "Send";
            this.Send.UseVisualStyleBackColor = true;
            this.Send.Click += new System.EventHandler(this.Button1_Click);
            // 
            // PastCommand
            // 
            this.PastCommand.Location = new System.Drawing.Point(33, 43);
            this.PastCommand.Multiline = true;
            this.PastCommand.Name = "PastCommand";
            this.PastCommand.Size = new System.Drawing.Size(726, 271);
            this.PastCommand.TabIndex = 2;
            this.PastCommand.TextChanged += new System.EventHandler(this.PastCommand_TextChanged);
            // 
            // PuppetMaster
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.PastCommand);
            this.Controls.Add(this.Send);
            this.Controls.Add(this.CommandBox);
            this.Name = "PuppetMaster";
            this.Text = "PuppetMaster";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox CommandBox;
        private System.Windows.Forms.Button Send;
        private System.Windows.Forms.TextBox PastCommand;
    }
}

