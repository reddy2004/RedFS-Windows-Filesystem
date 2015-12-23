using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;

namespace redfs_initiator
{
    public partial class Form1 : Form
    {
        private VDSDKLib.VirtualDrivesManager m_pManager;

        private TextBox[] iptextboxes = new TextBox[8];
        private TextBox[] porttextboxes = new TextBox[8];
        private TextBox[] mounttextboxes = new TextBox[8];
        private TextBox[] driveidtextboxes = new TextBox[8];
        private DiskItem[] interfaceList = new DiskItem[8];

        public Form1()
        {
            InitializeComponent();

            GLOBAL.icomm = new RedFSCommStack();

            // initialize VDSDK
            m_pManager = new VDSDKLib.VirtualDrivesManager();
            m_pManager.InitializeVDSDK();

            m_pManager.StartDriver();
            // Activate VDSDK.

            if (m_pManager.ActivateVDSDK("hSqJyCrAte8wq/MhrV/LP3NZauN11+Ogc1lq43XX46BzWWrjddfjoHNZauN11+Ogc1lq43XX46BzWWrjddfjoMi9TojOMYdxPLM0XJiTDDRzWWrjddfjoHNZauN11+Ogc1lq43XX46BzWWrjddfjoHNZauN11+Ogc1lq43XX46BdU3YDXignoHNZauN11+Ogc1lq43XX46BzWWrjddfjoHNZauN11+Ogc1lq43XX46BzWWrjddfjoHNZauN11+Ogz7x8DZHEEgt24X+gZIRozFL/u1w0ZGkGIPIEvcZBgPQg8gS9xkGA9CDyBL3GQYD0IPIEvcZBgPSWFR+cJnqL8Ne/CXwjZWxCOAOMIPe/NwWT6FHFuohURbMM3yMO+L0nHWMwvFoQHbj77kD1rWIDT7fFw88Mn9UZeR5tYddrVVLkgMia9MXI1uQ0LmepURP5+71j2Km7qRoRauxe5Zlsg05zzGQm4xs8PWujMBWFWWKgm486og1uKjwlCvK2cDqHqCeyZKslq/Wpq0Fr5aA6C2mqXrdOI/Yffda2FC8Kvug2t79thAVZoNoDIz0nA59IGE81mdLZzSTuGBlKh8WE0Ai0ZrTDBxGI+zZihHTWuHWbGupXwZGvvzf1KWaCSbWBpFKoakGVqDq8WA+GwUQ6bRTq7YCBaXGpZJ+Ty59FwgYZ423Oho60yKg2bAaz2Qit80BD767E8B6C7CBAAC5585D4B6501D6D") == 0)
            {
                // Activation error
                MessageBox.Show("Error Activation");
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            for (int i = 0; i < interfaceList.Length; i++) 
            {
                if (interfaceList[i] != null) 
                {
                    interfaceList[i].unlaunch(m_pManager);
                }
            }
        }



        private bool connect_click(int id)
        {
            //First validate
            try
            {
                IPAddress ipaddr = IPAddress.Parse(iptextboxes[id].Text);// Parse("192.168.1.1");
                int driveid = Int32.Parse(driveidtextboxes[id].Text);
                int port = Int32.Parse(porttextboxes[id].Text);

                RemoteVolumeInfo rvi = GLOBAL.icomm.connect(ipaddr, port, driveid);
                if (rvi == null) {
                    Console.WriteLine("failed to connect to remote redfs on " + ipaddr);
                    return false;
                }
                interfaceList[id] = new DiskItem(char.Parse(mounttextboxes[id].Text), rvi);
                interfaceList[id].launch(m_pManager);
            }
            catch (Exception e) 
            {
                Console.WriteLine("in connect_click : exception " + e.Message);
                MessageBox.Show("Error in parameters or Server not available");
                return false;
            }
            iptextboxes[id].Enabled = false;
            porttextboxes[id].Enabled = false;
            mounttextboxes[id].Enabled = false;
            driveidtextboxes[id].Enabled = false;
            return true;
        }

        private void disconnect_click(int id)
        {
            iptextboxes[id].Enabled = true;
            porttextboxes[id].Enabled = true;
            mounttextboxes[id].Enabled = true;
            driveidtextboxes[id].Enabled = true;

            interfaceList[id].unlaunch(m_pManager);
            interfaceList[id] = null;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            iptextboxes[0] = ip1;
            iptextboxes[1] = ip2;
            iptextboxes[2] = ip3;
            iptextboxes[3] = ip4;
            iptextboxes[4] = ip5;
            iptextboxes[5] = ip6;
            iptextboxes[6] = ip7;
            iptextboxes[7] = ip8;

            porttextboxes[0] = port1;
            porttextboxes[1] = port2;
            porttextboxes[2] = port3;
            porttextboxes[3] = port4;
            porttextboxes[4] = port5;
            porttextboxes[5] = port6;
            porttextboxes[6] = port7;
            porttextboxes[7] = port8;

            mounttextboxes[0] = mount1;
            mounttextboxes[1] = mount2;
            mounttextboxes[2] = mount3;
            mounttextboxes[3] = mount4;
            mounttextboxes[4] = mount5;
            mounttextboxes[5] = mount6;
            mounttextboxes[6] = mount7;
            mounttextboxes[7] = mount8;

            driveidtextboxes[0] = did1;
            driveidtextboxes[1] = did2;
            driveidtextboxes[2] = did3;
            driveidtextboxes[3] = did4;
            driveidtextboxes[4] = did5;
            driveidtextboxes[5] = did6;
            driveidtextboxes[6] = did7;
            driveidtextboxes[7] = did8;

            s1.Enabled = d1.Enabled = false;
            s2.Enabled = d2.Enabled = false;
            s3.Enabled = d3.Enabled = false;
            s4.Enabled = d4.Enabled = false;
            s5.Enabled = d5.Enabled = false;
            s6.Enabled = d6.Enabled = false;
            s7.Enabled = d7.Enabled = false;
            s8.Enabled = d8.Enabled = false;

            iptextboxes[0].Text = "192.168.1.100";
            porttextboxes[0].Text = "11111";
            mounttextboxes[0].Text = "T";
            driveidtextboxes[0].Text = "1";
        }

        private void c1_Click(object sender, EventArgs e)
        {
            if (connect_click(0))
            {
                c1.Enabled = false;
                d1.Enabled = true;
                s1.Enabled = true;
            }
        }

        private void c2_Click(object sender, EventArgs e)
        {
            if (connect_click(1))
            {
                c2.Enabled = false;
                d2.Enabled = true;
                s2.Enabled = true;
            }
        }

        private void c3_Click(object sender, EventArgs e)
        {
            if (connect_click(2))
            {
                c3.Enabled = false;
                d3.Enabled = true;
                s3.Enabled = true;
            }
        }

        private void c4_Click(object sender, EventArgs e)
        {
            if (connect_click(3))
            {
                c4.Enabled = false;
                d4.Enabled = true;
                s4.Enabled = true;
            }
        }

        private void c5_Click(object sender, EventArgs e)
        {
            if (connect_click(4))
            {
                c5.Enabled = false;
                d5.Enabled = true;
                s5.Enabled = true;
            }
        }

        private void c6_Click(object sender, EventArgs e)
        {
            if (connect_click(5))
            {
                c6.Enabled = false;
                d6.Enabled = true;
                s6.Enabled = true;
            }
        }

        private void c7_Click(object sender, EventArgs e)
        {
            if (connect_click(6))
            {
                c7.Enabled = false;
                d7.Enabled = true;
                s7.Enabled = true;
            }
        }

        private void c8_Click(object sender, EventArgs e)
        {
            if (connect_click(7))
            {
                c8.Enabled = false;
                d8.Enabled = true;
                s8.Enabled = true;
            }
        }

        private void d1_Click(object sender, EventArgs e)
        {
            c1.Enabled = true;
            d1.Enabled = false;
            s1.Enabled = false;
            disconnect_click(0);
        }

        private void d2_Click(object sender, EventArgs e)
        {
            c2.Enabled = true;
            d2.Enabled = false;
            s2.Enabled = false;
            disconnect_click(1);
        }

        private void d3_Click(object sender, EventArgs e)
        {
            c3.Enabled = true;
            d3.Enabled = false;
            s3.Enabled = false;
            disconnect_click(2);
        }

        private void d4_Click(object sender, EventArgs e)
        {
            c4.Enabled = true;
            d4.Enabled = false;
            s4.Enabled = false;
            disconnect_click(3);
        }

        private void d5_Click(object sender, EventArgs e)
        {
            c5.Enabled = true;
            d5.Enabled = false;
            s5.Enabled = false;
            disconnect_click(4);
        }

        private void d6_Click(object sender, EventArgs e)
        {
            c6.Enabled = true;
            d6.Enabled = false;
            s6.Enabled = false;
            disconnect_click(5);
        }

        private void d7_Click(object sender, EventArgs e)
        {
            c7.Enabled = true;
            d7.Enabled = false;
            s7.Enabled = false;
            disconnect_click(6);
        }

        private void d8_Click(object sender, EventArgs e)
        {
            c8.Enabled = true;
            d8.Enabled = false;
            s8.Enabled = false;
            disconnect_click(7);
        }

        private void scrub_click(int id)
        { 
        
        
        }

        private void s1_Click(object sender, EventArgs e)
        {
            s1.Enabled = false;
            //blocking
            scrub_click(1);
        }

        private void s2_Click(object sender, EventArgs e)
        {
            s2.Enabled = false;
            //blocking
            scrub_click(2);
        }

        private void s3_Click(object sender, EventArgs e)
        {
            s3.Enabled = false;
            //blocking
            scrub_click(3);
        }

        private void s4_Click(object sender, EventArgs e)
        {
            s4.Enabled = false;
            //blocking
            scrub_click(4);
        }

        private void s5_Click(object sender, EventArgs e)
        {
            s5.Enabled = false;
            //blocking
            scrub_click(5);
        }

        private void s6_Click(object sender, EventArgs e)
        {
            s6.Enabled = false;
            //blocking
            scrub_click(6);
        }

        private void s7_Click(object sender, EventArgs e)
        {
            s7.Enabled = false;
            //blocking
            scrub_click(7);
        }

        private void s8_Click(object sender, EventArgs e)
        {
            s8.Enabled = false;
            //blocking
            scrub_click(8);
        }
    }
}
