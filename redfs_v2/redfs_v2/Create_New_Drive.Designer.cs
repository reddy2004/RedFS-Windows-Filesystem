namespace redfs_v2
{
    partial class Create_New_Drive
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
            this.cancelb = new System.Windows.Forms.Button();
            this.saveexit = new System.Windows.Forms.Button();
            this.newdrivecomments = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.backinglabel = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.newlabel = new System.Windows.Forms.Label();
            this.newlabel1 = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // cancelb
            // 
            this.cancelb.Location = new System.Drawing.Point(178, 367);
            this.cancelb.Name = "cancelb";
            this.cancelb.Size = new System.Drawing.Size(75, 23);
            this.cancelb.TabIndex = 15;
            this.cancelb.Text = "Cancel";
            this.cancelb.UseVisualStyleBackColor = true;
            this.cancelb.Click += new System.EventHandler(this.cancelb_Click);
            // 
            // saveexit
            // 
            this.saveexit.Location = new System.Drawing.Point(259, 367);
            this.saveexit.Name = "saveexit";
            this.saveexit.Size = new System.Drawing.Size(111, 23);
            this.saveexit.TabIndex = 14;
            this.saveexit.Text = "Save and Exit";
            this.saveexit.UseVisualStyleBackColor = true;
            this.saveexit.Click += new System.EventHandler(this.saveexit_Click);
            // 
            // newdrivecomments
            // 
            this.newdrivecomments.Font = new System.Drawing.Font("Verdana", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.newdrivecomments.Location = new System.Drawing.Point(20, 131);
            this.newdrivecomments.Multiline = true;
            this.newdrivecomments.Name = "newdrivecomments";
            this.newdrivecomments.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.newdrivecomments.Size = new System.Drawing.Size(351, 229);
            this.newdrivecomments.TabIndex = 13;
            this.newdrivecomments.TextChanged += new System.EventHandler(this.newdrivecomments_TextChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(17, 114);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(140, 13);
            this.label3.TabIndex = 12;
            this.label3.Text = "Comments (1024 chars max)";
            // 
            // backinglabel
            // 
            this.backinglabel.Enabled = false;
            this.backinglabel.Location = new System.Drawing.Point(120, 48);
            this.backinglabel.Name = "backinglabel";
            this.backinglabel.Size = new System.Drawing.Size(251, 20);
            this.backinglabel.TabIndex = 11;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(40, 55);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(74, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "Backing Drive";
            // 
            // newlabel
            // 
            this.newlabel.AutoSize = true;
            this.newlabel.Location = new System.Drawing.Point(28, 26);
            this.newlabel.Name = "newlabel";
            this.newlabel.Size = new System.Drawing.Size(86, 13);
            this.newlabel.TabIndex = 9;
            this.newlabel.Text = "New Drive Label";
            // 
            // newlabel1
            // 
            this.newlabel1.Location = new System.Drawing.Point(120, 19);
            this.newlabel1.Name = "newlabel1";
            this.newlabel1.Size = new System.Drawing.Size(251, 20);
            this.newlabel1.TabIndex = 8;
            this.newlabel1.TextChanged += new System.EventHandler(this.newlabel1_TextChanged);
            // 
            // Create_New_Drive
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(389, 408);
            this.Controls.Add(this.cancelb);
            this.Controls.Add(this.saveexit);
            this.Controls.Add(this.newdrivecomments);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.backinglabel);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.newlabel);
            this.Controls.Add(this.newlabel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(395, 436);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(395, 436);
            this.Name = "Create_New_Drive";
            this.Text = "Create_New_Drive";
            this.Load += new System.EventHandler(this.Create_New_Drive_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cancelb;
        private System.Windows.Forms.Button saveexit;
        private System.Windows.Forms.TextBox newdrivecomments;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox backinglabel;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label newlabel;
        private System.Windows.Forms.TextBox newlabel1;
    }
}