using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace processManage
{
    public partial class Form2 : Form
    {
        int num = 0;    //约定进程名
        List<PCB> ready_queue = new List<PCB>();    //就绪队列
        List<PCB> interrupt_queue = new List<PCB>();    //阻塞队列
        memory_Manage[] memory = new memory_Manage[18];     //定义内存块数组，一共18块
        List<memory_Manage> unuse_memory = new List<memory_Manage>();
        List<PCB> queue = new List<PCB>();  //PCB表
        string PC;     //记录此时运行到哪个进程PCB
        string IR;    //记录此时运行的进程中的执行的命令
        PCB cpu_pcb = null;     //定义使用cpu的pcb
        PCB idle = new PCB();   //闲逛进程
        int count = 0;

        public Form2()
        {
            InitializeComponent();
            process_name.Enabled = false;
            process_dengji.Enabled = false;
            instruction.Enabled = false;
            middle_result.Enabled = false;
            mm.Enabled = false;
            time_slice.Enabled = false;
            local_time.Enabled = false;
            textBox1.Enabled = false;
            select_button.Enabled = false;
            begin_exe.Enabled = false;
        }


        private void open_button_Click(object sender, EventArgs e)      //开机时，控件才可以使用
        {
            select_button.Enabled = true;
            begin_exe.Enabled = true;
        }

        private void close_button_Click(object sender, EventArgs e)     //关机时，无法选择文件
        {
            select_button.Enabled = false;
            begin_exe.Enabled = false;
            System.Environment.Exit(0);
        }


        private void Timer1_Tick(object sender, EventArgs e)        //当前时间定时器
        {
            DateTime dt = DateTime.Now;
            string date = dt.ToLongTimeString();
            local_time.Text = date.ToString();
        }
        private void Form2_Load(object sender, EventArgs e)
        {
            Control.CheckForIllegalCrossThreadCalls = false;
            for (int i = 0; i < memory.Length; i++)  //初始化内存块
            {
                memory[i] = new memory_Manage();
                memory[i].size = 5 * (i + 1);
                memory[i].index = i;
            }
        }
        //退出按键
        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult dr = MessageBox.Show("是否退出?", "提示:", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (dr == DialogResult.OK)   //如果单击“是”按钮
            {
                e.Cancel = false;                 //关闭窗体
                System.Environment.Exit(0);
            }
            else if (dr == DialogResult.Cancel)
            {
                e.Cancel = true;                  //不执行操作
            }
        }
        private void select_button_Click(object sender, EventArgs e)
        {
            count++;
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Multiselect = true;     //可以选择多个文件
            dialog.Title = "请选择文件";
            dialog.Filter = "所有文件(*.txt)|*.txt";
            string filename = string.Empty;
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                filename = dialog.FileName;     //获取文件路径
                foreach(string file in dialog.FileNames)
                {
                    StreamReader sr = new StreamReader(file);
                    string line;
                    string str=string.Empty;      //将每个文件中的数据读出
                    while ((line = sr.ReadLine()) != null)
                    {
                        str = str + line;
                    }
                    create_process(str);    //给此文件创建进程
                    sr.Close();
                }
            }
            add_process();      //刷新就绪队列，从pcb表中添加进程到就绪队列
            //if (filename != "")
            // {
            // string str = File.ReadAllText(filename);//读取路径下txt文件的内容
            // textBox1.Text = str;
            // create_process(str);//为此文件创建一个进程

            //}
        }
        private void create_process(string str)     //创建进程控制块
        {
            if (ready_queue.Count>=1&&queue[0].processID == -1)   //闲逛进程
            {
                queue.RemoveAt(0);
                ready_queue.RemoveAt(0);
                cpu_pcb = null;
            }
            PCB pcb = new PCB();    //创建一个进程控制块
            pcb.gettxt(str);//将文件中的命令解析后放入数组
            pcb.processID = num;//赋予唯一的进程标识
            pcb.processName = "process" + num;  //使用该进程控制块的进程名
            pcb.dengji = pcb.IR.Length;       //该进程的优先级即为其中命令的数量，命令少的优先级高
            pcb.memory_use = new Random().Next(1 , 90);     //生成一个1到90的随机数分配给此pcb的内存使用
            num++;
            queue.Add(pcb);     //将进程添加到PCB表中
            sort_memory();
            if (unuse_memory.Count == 0 || (unuse_memory.Count > 0 && unuse_memory[unuse_memory.Count - 1].size < pcb.memory_use))
            {
                MessageBox.Show("对应内存不足，无法分配");
                System.Environment.Exit(0);
            }
            else
            {
                foreach (memory_Manage m in unuse_memory)
                {
                    if (m.size >= pcb.memory_use)
                    {
                        memory[m.index].flag = 1;
                        memory_use.Value += m.size;
                        pcb.index = m.index;
                        changepannel(pcb.index);
                        break;
                    }
                }
            } 
        }
        
        private int execution(PCB pcb, string ccmd)
        {
            if (ccmd.Contains("="))     //赋值语句
            {
                pcb.DR = Convert.ToInt32(ccmd.Substring(2)); //将ccmd的数字提取出来赋值给当前进程的变量x
                return 0;
            }
            else if (ccmd.Contains("++"))   //自增语句
            {
                pcb.DR = pcb.DR + 1;
                return 0;
            }
            else if (ccmd.Contains("--"))   //自减语句
            {
                pcb.DR = pcb.DR - 1;
                return 0;
            }
            else if (ccmd.Contains("!"))    //阻塞
            {
                pcb.interrupt_time = Convert.ToInt32(ccmd.Substring(2));    //阻塞时间
                pcb.PSW = int.Parse(ccmd.Substring(1, 1));    //阻塞原因
                switch (pcb.PSW)
                {
                    case 1:
                        pcb.interrupt_reason = "时钟中断";
                        break;
                    case 2:
                        pcb.interrupt_reason = "输入输出中断";
                        break;
                    case 4:
                        pcb.interrupt_reason = "软中断";
                        break;
                }
                return 1;
            }
            return 10;
        }
        private void CPU()      //cpu模拟
        {
            while (true)        //cpu一直在工作
            {
                //add_process();      //从pcb表添加进程到就绪队列
                for (int i = 0; i < interrupt_queue.Count;)      //循环遍历阻塞队列
                {
                    if (interrupt_queue[i].interrupt_time == 0)     //如果pcb的阻塞时间为0
                    {
                        //interrupt_queue[i].PSW = 0;     //设置为就绪态
                        if (ready_queue.Count ==1 && ready_queue[0].processID ==-1)
                        {
                            ready_queue.RemoveAt(0);
                            queue.RemoveAt(0);
                            cpu_pcb = null;
                        }
                        ready_queue.Add(interrupt_queue[i]);    //将它加入到就绪队列
                        listView1.Items.RemoveAt(i);
                        queue.Add(interrupt_queue[i]);
                        //queue.Sort(new pcb_compare());      //对pcb表排序
                        interrupt_queue.RemoveAt(i);
                        add_process();      //刷新
                    }
                    else        //如果进程的阻塞时间不为0
                    {
                        interrupt_queue[i].interrupt_time--;    //让阻塞时间减一
                        listView1.Items[i].SubItems[3].Text = interrupt_queue[i].interrupt_time.ToString();//修改阻塞时间
                        i++;
                    }
                    
                }
                if (ready_queue.Count != 0 )   //如果就绪队列不为空
                {
                    if (cpu_pcb == null)    //如果当前cpu没有被占用
                    {
                        cpu_pcb = queue[0];     //取出pcb表中第一个进程控制块，表示正在运行，此时cpu_pcb!=null
                        listView3.Items[0].Remove();
                        PC = System.Text.RegularExpressions.Regex.Replace(cpu_pcb.processName, @"[^0-9]+", "");//提取进程名中的数字赋值给PC
                        //IR = cpu_pcb.IR;  //当前执行指令数组
                        //if (cpu_pcb.PSW > 0)    //有中断
                        //{
                           // interrupt_progress();   //转入中断处理程序
                        //}
                    }
                    if (cpu_pcb.time_pice > 0)
                    {
                        IR = cpu_pcb.IR[cpu_pcb.PC];    //指令寄存器中存取当前执行的指令
                        int i= execution(cpu_pcb, IR);
                        cpu_pcb.time_pice--;    //每执行一条语句时间片减1
                        process_name.Text = cpu_pcb.processName;
                        process_dengji.Text = cpu_pcb.dengji + "";
                        textBox1.Text = cpu_pcb.txtcmd;
                        mm.Text = cpu_pcb.memory_use.ToString();
                        time_slice.Text = cpu_pcb.time_pice.ToString();
                        instruction.Text = IR;  //将当前执行放入执行中的指令
                        if (i == 0)    //如果当前指令的返回结果是0，说明是正常指令
                        {
                            middle_result.Text = cpu_pcb.DR.ToString();      //将每次执行完的结果放入中间结果
                            cpu_pcb.PC++;       //指向下一条指令
                        }
                        else if (i == 1)   //说明是中断语句
                        {
                            cpu_pcb.PSW = 2;
                            interrupt_progress();//转入中断事件处理程序
                        }
                        else if (i == 10)      //说明全部命令执行完成
                        {
                            cpu_pcb.PSW = 4;    //程序结束的软中断
                            interrupt_progress();
                            if (listView2.Items.Count > 12)
                            {
                                listView2.Items.Clear();
                            }
                        }
                    }
                    else    //时间片用完
                    {
                        cpu_pcb.PSW = 1;
                        interrupt_progress();
                    }
                }
                else    //就绪队列为空
                {
                    //Delay(3000);
                    idle.gettxt("x=0;x=0;x=0;end");
                    idle.processID = -1;
                    idle.processName = "idle";
                    idle.dengji = 10;
                    queue.Add(idle);    //将闲逛进程添加到pcb表
                    idle.time_pice = 100;
                    add_process();
                }
                //Thread.Sleep(1000);
                Delay(3000);
            }   
        }
        static int sort_pcb(PCB a,PCB b)     //将pcb表中的进程按优先级排序
        {
            if (a.dengji > b.dengji)
            {
                return 1;       //b的优先级更高
            }
            else if (a.dengji == b.dengji)
            {
                return a.processID.CompareTo(b.processID);      //按进程标识排序，先进来的优先级高
            }
            else
                return -1;      //a的优先级更高
        }
        class pcb_compare : IComparer<PCB>  //实现自定义排序接口
         {
            public int Compare(PCB x, PCB y)
            {
                return sort_pcb(x, y);
            }
         }
        private void add_process()
        {
            listView3.Items.Clear();    //清空窗体上的就绪队列，有更新就绪队列的效果
            ready_queue.Clear();
            queue.Sort(new pcb_compare());      //对pcb表排序
            foreach (PCB pcb in queue)       //遍历pcb表
            {
                 ListViewItem item = new ListViewItem();//创建一个新的行
                 item.Text = pcb.processName;        //将进程名添加到第一列
                 item.SubItems.Add("" + pcb.dengji);      //将进程的优先级添加到第二列
                 listView3.Items.Add(item);   //将item加入到就绪队列中
                 ready_queue.Add(pcb);   //将这个进程添加到就绪队列
            }
        }
        private void interrupt_progress()   //阻塞处理
        {
            switch (cpu_pcb.PSW)
            {
                case 1:     //时钟中断
                    {
                        ready_queue.Remove(cpu_pcb);
                        queue.Remove(cpu_pcb);
                        queue.Add(cpu_pcb);
                        add_process();
                        cpu_pcb.time_pice = 6;
                        cpu_pcb = null;
                    }
                    break;
                case 2:     //I/O中断
                    {
                        ListViewItem viewItem = new ListViewItem();
                        viewItem.Text = cpu_pcb.processName;
                        viewItem.SubItems.Add(cpu_pcb.dengji + "");
                        viewItem.SubItems.Add(cpu_pcb.interrupt_reason);
                        viewItem.SubItems.Add(cpu_pcb.interrupt_time.ToString());
                        listView1.Items.Add(viewItem);
                        interrupt_queue.Add(cpu_pcb);   //将当前队列加入到阻塞队列
                        PCB pcb=null;
                        foreach(PCB pcb2 in ready_queue)
                        {
                            if (pcb2.processID == cpu_pcb.processID)
                            {
                                pcb = pcb2;
                                break;
                            }
                        }
                        ready_queue.Remove(pcb);
                        queue.Remove(pcb);
                        cpu_pcb.PC++;
                        cpu_pcb = null; //让出cpu
                    }
                    break;
                case 4:     //软中断
                    {
                        if (cpu_pcb.processID == -1)
                        {
                            cpu_pcb.PC = 0;
                        }
                        else
                        {
                            ready_queue.RemoveAt(0);    //就把第一个优先级最高的进程控制块取走
                            ListViewItem viewItem = new ListViewItem();
                            viewItem.Text = cpu_pcb.processName;
                            viewItem.SubItems.Add(cpu_pcb.DR.ToString());
                            listView2.Items.Add(viewItem);
                            queue.RemoveAt(0);      //从pcb表中删除这个pcb
                            memory_use.Value = memory_use.Value - cpu_pcb.memory_use;   //释放cpu_pcb所占用的内存
                            memory[cpu_pcb.index].flag = 0;
                            changepannel(cpu_pcb.index);
                            cpu_pcb = null; //让出cpu
                        }
                        //time_pice = 6;  //重置时间片
                    }
                    break;
            }
        }
        private void begin_exe_Click(object sender, EventArgs e)
        {
            CPU();
        }
        #region 毫秒延时 界面不会卡死
        public static void Delay(int mm)
        {
            DateTime current = DateTime.Now;
            while (current.AddMilliseconds(mm) > DateTime.Now)
            {
                Application.DoEvents();
            }
            return;
        }
        #endregion
        private void sort_memory()     //排序未使用的内存块
        {
            int j = 0;
            unuse_memory.Clear();
            for(int i = 0; i < memory.Length; i++)
            {
                if (memory[i].flag == 0)
                {
                    unuse_memory.Add(memory[i]);
                    unuse_memory[j].flag = 0;
                    unuse_memory[j].size = memory[i].size;
                    unuse_memory[j].index = i;//memory[i]是空闲块
                    j++;
                }  
            }
            //unuse_memory.Sort();    //从小到大排序
            unuse_memory.Sort(delegate (memory_Manage x, memory_Manage y)
            {
                if (x.size > y.size)
                {
                    return 1;
                }
                else if (x.size == y.size)
                    return x.index.CompareTo(y.index);
                else
                    return -1;
            });
            
        }
        private void changepannel(int index)
        {
            foreach (Control s in this.groupBox2.Controls)
            {
                 if(s is Panel) 
                 {
                     if (memory[index].flag == 1)
                     {
                         if (s.Name.Equals("panel" + index))
                         {
                             s.BackColor = Color.Firebrick;
                            break;
                         }
                     }
                     else
                     {
                         if (s.Name.Equals("panel" + index))
                         {
                             s.BackColor = Color.LimeGreen;
                            break;
                         }
                     }
                 }
            }
        }
    }
}
