import time
import pyvisa
import matplotlib.pyplot as plt
import openpyxl
import numpy as np
from matplotlib.ticker import FuncFormatter

# 设置matplotlib中文字体支持
plt.rcParams['font.sans-serif'] = ['SimHei', 'Microsoft YaHei', 'DejaVu Sans']
plt.rcParams['axes.unicode_minus'] = False

# ==================== 测试参数配置 ====================
START_FREQ = 10000      # 起始频率 10 kHz
STOP_FREQ = 100000000     # 截止频率 100 MHz
NUM_POINTS = 100         # 对数扫描点数
AWG_VOLTAGE = 2         # AWG 输出电压 2Vpp
INPUT_POWER_DBM = 10 * np.log10((AWG_VOLTAGE/2)**2 / 0.1)  # 输入功率，50欧匹配
SETTLE_TIME = 0.05      # 硬件切换后的稳定等待时间 (秒)
# ======================================================

# 1. 初始化 VISA 资源管理器
rm = pyvisa.ResourceManager()
resources = rm.list_resources()

print("发现的硬件设备列表:")
for i, res in enumerate(resources):
    print(f" [{i}] {res}")

# 2. 绑定仪器 (根据实际识别到的设备号或IP进行修改)
# 提示：可以在下面直接填入仪器的实际 VISA 地址，例如 'USB0::0xF4EC::0xEE38::...'
try:
    # 请根据上面打印出的资源列表，将索引修改为正确的硬件
    awg = rm.open_resource("USB0::0xF4EC::0x1102::SDG2XFBCA00677::INSTR")  # 信号源
    spec = rm.open_resource("USB0::0xF4EC::0x1305::SSA3PA2XA00499::INSTR") # 频谱仪
    
    # 设置超时时间
    awg.timeout = 5000
    spec.timeout = 5000
    
    print("\n成功连接仪器:")
    print(" 信号源:", awg.query("*IDN?").strip())
    print(" 频谱仪:", spec.query("*IDN?").strip())
except Exception as e:
    print(f"仪器连接失败，请检查连接或修改资源地址! 错误: {e}")
    exit()

# 3. 初始化仪器状态 (确保工作在最佳低频大动态状态)
print("\n正在初始化仪器参数...")
# 信号源初始化
awg.write("C1:OUTP OFF")
awg.write("C1:BSWV WVTP,SINE")               # 设置为正弦波
awg.write(f"C1:BSWV AMP,{AWG_VOLTAGE}")      # 设置大摆幅输出
awg.write("C1:OUTP LOAD,50")                 # 强制匹配 50 欧姆阻抗

# 频谱仪初始化 (进入点频窄带接收状态)
spec.write("INIT:CONT OFF")                  # 切换到单次测量模式模式 (Single)
spec.write("BAND:RES 10")                    # 核心：强行锁死 RBW = 10Hz，极限压低底噪！
spec.write("BAND:VID 10")                    # VBW = 10Hz
spec.write("POW:ATT 0")                      # 核心：测量阶段衰减器设为 0dB，全力探底
spec.write("SENS:DET:POW AVER")              # 采用平均值检波器，曲线更平滑

# 开启信号源输出
awg.write("C1:OUTP ON")

# 4. 动态多次测量循环 - 每次测量不同的电路，完成后询问是否继续
freq_array = np.logspace(np.log10(START_FREQ), np.log10(STOP_FREQ), NUM_POINTS)
all_measurements = []  # 存储所有测量数据
measurement_num = 0
continue_measuring = True

while continue_measuring:
    measurement_num += 1
    
    # 在每次测量前暂停，要求用户切换电路
    if measurement_num > 1:
        print("\n" + "="*60)
        print(f"⚠️  准备进行第 {measurement_num} 次测量")
        print("="*60)
    else:
        print("\n" + "="*60)
        print(f"开始第 {measurement_num} 次测量")
        print("="*60)
    
    print("请确保已连接要测试的电路")
    input("按 Enter 继续...")
    
    # 请求用户输入电路标签
    default_label = f"Circuit_{measurement_num}"
    user_label = input(f"请输入此电路的标签 (默认: {default_label}): ").strip()
    circuit_name = user_label if user_label else default_label
    
    print(f"\n测量对象: {circuit_name}")
    
    freqs = []
    output_powers = []
    attenuations = []
    
    print(f"{'频率(Hz)':<15}{'输出功率(dBm)':<15}{'衰减(dB)':<15}")
    print("-" * 50)
    
    try:
        for current_freq in freq_array:
            current_freq = int(current_freq)
            # A. 改变信号源频率
            awg.write(f"C1:BSWV FRQ,{current_freq}")
            
            # B. 同步修改频谱仪的中心频率
            spec.write(f"SENS:FREQ:CENT {current_freq}")
            spec.write(f"SENS:FREQ:SPAN 100") # 只看中心点周围 100Hz 的窄跨度
            
            # C. 等待硬件稳定
            time.sleep(SETTLE_TIME)
            
            # D. 触发频谱仪进行单次刷新测量
            spec.write("INIT:IMM")
            spec.query("*OPC?") # 等待扫描彻底结束
            
            # E. 读取该频点处的最高能量尖峰
            spec.write("CALCulate:MARKer1:MAXimum")     
            time.sleep(0.01)
            peak_str = spec.query("CALCulate:MARKer1:Y?")
            output_power = float(peak_str)
            
            # 计算衰减值
            attenuation = INPUT_POWER_DBM - output_power
            
            # 记录数据
            freqs.append(current_freq)
            output_powers.append(output_power)
            attenuations.append(attenuation)
            
            print(f"{current_freq:<15}{output_power:<15.2f}{attenuation:<15.2f}")
        
        # 保存该次测量的数据
        all_measurements.append({
            'measurement_num': measurement_num,
            'circuit_name': circuit_name,
            'freqs': freqs,
            'output_powers': output_powers,
            'attenuations': attenuations
        })
        
        print(f"\n✓ 第 {measurement_num} 次测量完成（{circuit_name}）")
        
        # 询问是否继续测量
        while True:
            response = input("\n是否继续测量下一个电路? (y/n): ").strip().lower()
            if response in ['y', 'yes', '是']:
                continue_measuring = True
                break
            elif response in ['n', 'no', '否']:
                continue_measuring = False
                break
            else:
                print("请输入 y 或 n")
        
    except Exception as e:
        print(f"测量 {measurement_num} 出错: {e}")
        continue_measuring = False
        break

# 测试完毕后安全关闭仪器
try:
    awg.write("C1:OUTP OFF")
finally:
    rm.close()

# 5. 数据保存与绘图
print("\n测试完成。正在生成报告...")

# 保存至 Excel（创建多个工作表，每个工作表对应一次测量，包含电路标签）
wb = openpyxl.Workbook()
wb.remove(wb.active)  # 删除默认工作表

for measurement in all_measurements:
    circuit_name = measurement.get('circuit_name', f"Circuit_{measurement['measurement_num']}")
    ws = wb.create_sheet(circuit_name[:31])  # Excel sheet name最多31字符
    ws.append(["频率 (Hz)", "输出功率 (dBm)", "衰减 (dB)"])
    for f, p, a in zip(measurement['freqs'], measurement['output_powers'], measurement['attenuations']):
        ws.append([f, p, a])

wb.save("Filter_Insertion_Loss_Report.xlsx")
print(" 数据已成功保存至: Filter_Insertion_Loss_Report.xlsx")

# 绘制衰减曲线（对数坐标，所有不同电路的测量在同一张图对比）
def freq_formatter(x, pos):
    if x >= 1000:
        return f'{int(x/1000)}M'
    elif x >= 1:
        return f'{int(x)}k'
    else:
        return f'{x:.0f}'

freq_min_khz = START_FREQ / 1000
freq_max_khz = STOP_FREQ / 1000
plt.figure(figsize=(14, 8))

# 定义颜色列表
colors = ['red', 'blue', 'green', 'orange', 'purple', 'brown', 'pink', 'gray', 'cyan', 'magenta']

# 绘制所有电路的测量曲线
for i, measurement in enumerate(all_measurements):
    color = colors[i % len(colors)]
    freqs_khz = [f/1000 for f in measurement['freqs']]
    circuit_label = measurement.get('circuit_name', f"Circuit_{measurement['measurement_num']}")
    plt.semilogx(freqs_khz, measurement['attenuations'], 
                 color=color, linewidth=2.5, marker='o', markersize=6, 
                 label=circuit_label, alpha=0.8)

plt.title(f"滤波器衰减对比 ({freq_min_khz:.0f}kHz - {freq_max_khz:.0f}MHz)", 
          fontsize=14, fontweight='bold')
plt.xlabel("频率 (kHz, 对数)", fontsize=12)
plt.ylabel("衰减 (dB)", fontsize=12)
plt.xlim(freq_min_khz, freq_max_khz)
plt.ylim(0, 120)
plt.gca().xaxis.set_major_formatter(FuncFormatter(freq_formatter))
plt.grid(True, which="both", linestyle="--", alpha=0.7)
plt.legend(fontsize=11, loc='best', framealpha=0.95)
plt.tight_layout()
plt.savefig("Filter_Curve.png", dpi=300)
print(" 衰减曲线已成功保存至: Filter_Curve.png")
plt.show()
