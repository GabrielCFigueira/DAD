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
            this.button2 = new System.Windows.Forms.Button();
            this.Status = new System.Windows.Forms.Button();
            this.Crash = new System.Windows.Forms.Button();
            this.Freeze = new System.Windows.Forms.Button();
            this.Unfreeze = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // CommandBox
            // 
            this.CommandBox.Location = new System.Drawing.Point(44, 431);
            this.CommandBox.Margin = new System.Windows.Forms.Padding(4);
            this.CommandBox.Name = "CommandBox";
            this.CommandBox.Size = new System.Drawing.Size(967, 31);
            this.CommandBox.TabIndex = 0;
            this.CommandBox.Text = "Input";
            this.CommandBox.TextChanged += new System.EventHandler(this.TextBox1_TextChanged);
            // 
            // Send
            // 
            this.Send.Location = new System.Drawing.Point(352, 471);
            this.Send.Margin = new System.Windows.Forms.Padding(4);
            this.Send.Name = "Send";
            this.Send.Size = new System.Drawing.Size(311, 64);
            this.Send.TabIndex = 1;
            this.Send.Text = "Send";
            this.Send.UseVisualStyleBackColor = true;
            this.Send.Click += new System.EventHandler(this.Button1_Click);
            // 
            // PastCommand
            // 
            this.PastCommand.Location = new System.Drawing.Point(44, 54);
            this.PastCommand.Margin = new System.Windows.Forms.Padding(4);
            this.PastCommand.Multiline = true;
            this.PastCommand.Name = "PastCommand";
            this.PastCommand.Size = new System.Drawing.Size(967, 338);
            this.PastCommand.TabIndex = 2;
            this.PastCommand.TextChanged += new System.EventHandler(this.PastCommand_TextChanged);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(1100, 76);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(204, 54);
            this.button2.TabIndex = 4;
            this.button2.Text = "AddRoom";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // Status
            // 
            this.Status.Location = new System.Drawing.Point(1103, 187);
            this.Status.Name = "Status";
            this.Status.Size = new System.Drawing.Size(200, 46);
            this.Status.TabIndex = 5;
            this.Status.Text = "Status";
            this.Status.UseVisualStyleBackColor = true;
            this.Status.Click += new System.EventHandler(this.Status_Click);
            // 
            // Crash
            // 
            this.Crash.Location = new System.Drawing.Point(1103, 284);
            this.Crash.Name = "Crash";
            this.Crash.Size = new System.Drawing.Size(200, 45);
            this.Crash.TabIndex = 6;
            this.Crash.Text = "Crash";
            this.Crash.UseVisualStyleBackColor = true;
            this.Crash.Click += new System.EventHandler(this.Crash_Click);
            // 
            // Freeze
            // 
            this.Freeze.Location = new System.Drawing.Point(1108, 359);
            this.Freeze.Name = "Freeze";
            this.Freeze.Size = new System.Drawing.Size(194, 48);
            this.Freeze.TabIndex = 7;
            this.Freeze.Text = "Freeze";
            this.Freeze.UseVisualStyleBackColor = true;
            this.Freeze.Click += new System.EventHandler(this.Freeze_Click);
            // 
            // Unfreeze
            // 
            this.Unfreeze.Location = new System.Drawing.Point(1108, 439);
            this.Unfreeze.Name = "Unfreeze";
            this.Unfreeze.Size = new System.Drawing.Size(196, 45);
            this.Unfreeze.TabIndex = 8;
            this.Unfreeze.Text = "Unfreeze";
            this.Unfreeze.UseVisualStyleBackColor = true;
            this.Unfreeze.Click += new System.EventHandler(this.Unfreeze_Click);
            // 
            // PuppetMaster
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1767, 864);
            this.Controls.Add(this.Unfreeze);
            this.Controls.Add(this.Freeze);
            this.Controls.Add(this.Crash);
            this.Controls.Add(this.Status);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.PastCommand);
            this.Controls.Add(this.Send);
            this.Controls.Add(this.CommandBox);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "PuppetMaster";
            this.Text = "PuppetMaster";
            this.Load += new System.EventHandler(this.PuppetMaster_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox CommandBox;
        private System.Windows.Forms.Button Send;
        private System.Windows.Forms.TextBox PastCommand;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button Status;
        private System.Windows.Forms.Button Crash;
        private System.Windows.Forms.Button Freeze;
        private System.Windows.Forms.Button Unfreeze;
    }
}

