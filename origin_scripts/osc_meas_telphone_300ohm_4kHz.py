import time
import numpy as np
import pyvisa
import matplotlib.pyplot as plt
from matplotlib.ticker import FuncFormatter, ScalarFormatter
import matplotlib as mpl

# 解决中文乱码问题
mpl.rcParams['font.sans-serif'] = ['SimHei', 'DejaVu Sans']
mpl.rcParams['axes.unicode_minus'] = False
# 禁用科学计数法
mpl.rcParams['axes.formatter.useoffset'] = False

# ==================== 参数配置 ====================
START_FREQ = 300      # 起始频率 Hz
STOP_FREQ = 20000     # 截止频率 Hz
STEPS = 50            # 扫描频点数
AWG_VOLTAGE = 20     # AWG 设定输出幅度 (Vpp)
INITIAL_SCALE_DELAY = 0.2      # 垂直档位调整后延时 (秒)
AUTOSET_DELAY = 0.4            # 自动对齐延时 (秒)

# 自动生成对数步进的频率列表（适合音频频响测试）
freq_list = np.logspace(np.log10(START_FREQ), np.log10(STOP_FREQ), STEPS)

# ==================== VISA 设备初始化 ====================
rm = pyvisa.ResourceManager()
resources = rm.list_resources()
# 打印所有连接的设备资源名称，方便你复制替换
print("正在扫描连接的仪器...")
print(rm.list_resources())

# ❗请将下方的地址替换为你电脑扫描出的实际 VISA 地址
# 例如: 'USB0::0x0699::0x03C7::C010101::INSTR'
try:
    osc = rm.open_resource(resources[1]) # 泰克 TBS2000
    awg = rm.open_resource(resources[0]) # 你的AWG
except Exception as e:
    print(f"设备连接失败，请检查地址。错误信息: {e}")
    exit()

osc.timeout = 5000
awg.timeout = 5000

print(f"成功连接示波器: {osc.query('*IDN?').strip()}")
print(f"成功连接波形发生器: {awg.query('*IDN?').strip()}")

# ==================== 仪器基础初始化 ====================
osc.write("CH1:SELect OFF")
osc.write("CH2:SELect ON")
osc.write("CH2:COUPLing AC")
osc.write("MEASUrement:IMMed:SOURce1 CH2")
osc.write("MEASUrement:IMMed:TYPe AMPlitude")

awg.write("C1:OUTP LOAD,HZ") # 对应面板的 Hi-Z 模式
awg.write("C1:BSWV WVTP,SINE")
awg.write(f"C1:BSWV AMP,{AWG_VOLTAGE}")

# ==================== 第一步：直通校准 (Thru) ====================
input("\n【第一步：直通校准】\n请将 AWG(串联250Ω后) 直接连接到 示波器CH2(并联300Ω到地)。\n连接完成后，按回车键开始校准...")

awg.write("C1:OUTP ON")
v_ref_list = []

print(f"\n{'频率 (Hz)':<12}{'AWG设置 V_set (V)':<18}{'直通测得 V_ref (V)':<18}{'直通自身衰减 (dB)':<18}")
print("-" * 70)

for freq in freq_list:
    awg.write(f"C1:BSWV FRQ,{freq}")
    period = 1.0 / freq
    # 1. 调整水平时基
    osc.write(f"HORizontal:SCAle {period * 2 / 10}")
    
    # 2. 【新增】动态优化 CH2 的垂直档位，防止量化卡死在 0.5V/1V
    osc.write("CH2:SCAle 0.2") # 先给个合理的初始中等档位防止爆音
    time.sleep(INITIAL_SCALE_DELAY)
    # 针对泰克 TBS2000，可以通过命令让其自动调整垂直以适应当前波形
    # 或者通过代码自适应调整（如下行，建议留出足够稳定时间）
    osc.write("AUTOSet EXECute") 
    time.sleep(AUTOSET_DELAY)  # 给予泰克示波器充足的自动对齐时间
    
    # 读取直通状态下 CH2 的电压
    v_ref = float(osc.query("MEASUrement:IMMed:VALue?").strip())
    v_ref_list.append(v_ref)
    
    # 计算直通电路本身的绝对分压衰减 (相对于AWG面板设置值)
    if v_ref > 0:
        thru_loss_db = 20 * np.log10(2*v_ref / AWG_VOLTAGE)
    else:
        thru_loss_db = -999
        
    print(f"{freq:<12.1f}{AWG_VOLTAGE:<18.2f}{v_ref:<18.4f}{thru_loss_db:<18.2f}")

print("-" * 70)
print("直通校准完成，基准线已存储。")
awg.write("C1:OUTP OFF")

# ==================== 第二步及后续：滤波器测量循环 ====================
def perform_filter_measurement(awg, osc, freq_list, v_ref_list, AWG_VOLTAGE, INITIAL_SCALE_DELAY, AUTOSET_DELAY):
    """执行一次滤波器测量"""
    results = []
    
    print(f"\n{'频率 (Hz)':<12}{'直通 V_ref (V)':<15}{'滤波器 V_out (V)':<18}{'滤波器纯净插损 (dB)':<18}")
    print("-" * 70)
    
    for i, freq in enumerate(freq_list):
        awg.write(f"C1:BSWV FRQ,{freq}")
        period = 1.0 / freq
        osc.write(f"HORizontal:SCAle {period * 2 / 10}")
        
        # 动态优化 CH2 的垂直档位，防止量化卡死在 0.5V/1V
        osc.write("CH2:SCAle 0.2")
        time.sleep(INITIAL_SCALE_DELAY)
        osc.write("AUTOSet EXECute") 
        time.sleep(AUTOSET_DELAY)
        
        # 读取接入滤波器后 CH2 的实际电压
        v_out = float(osc.query("MEASUrement:IMMed:VALue?").strip())
        v_ref = v_ref_list[i]
        
        # 计算纯净插入损耗 (扣除了直通分压后的净值)
        if v_out > 0 and v_ref > 0:
            filter_loss_db = 20 * np.log10(v_out / v_ref)
        else:
            filter_loss_db = -999
            
        print(f"{freq:<12.1f}{v_ref:<15.4f}{v_out:<18.4f}{filter_loss_db:<18.2f}")
        results.append((freq, v_ref, v_out, filter_loss_db))
    
    return results

# 生成频率格式化函数
def freq_formatter(x, pos):
    # x 的单位是 Hz
    if x >= 1000:
        return f'{int(x/1000)}k'
    else:
        return f'{int(x)}'

# 绘制和保存曲线函数
def plot_and_save_measurement(results, v_ref_list, AWG_VOLTAGE, measurement_number):
    """为一次测量生成并保存曲线"""
    thru_loss_db_list = []
    raw_loss_db_list = []
    calibrated_loss_db_list = []
    
    for i, (freq, v_ref, v_out, filter_loss_db) in enumerate(results):
        # 直通自身衰减
        thru_loss = 20 * np.log10(2 * v_ref_list[i] / AWG_VOLTAGE) if v_ref_list[i] > 0 else -999
        thru_loss_db_list.append(thru_loss)
        
        # 未校准的衰减
        if v_out > 0:
            raw_loss = 20 * np.log10(2 * v_out / AWG_VOLTAGE)
        else:
            raw_loss = -999
        raw_loss_db_list.append(raw_loss)
        
        # 校准后的衰减
        calibrated_loss_db_list.append(filter_loss_db)
    
    plt.figure(figsize=(12, 7))
    freq_hz = [r[0] for r in results]
    
    plt.semilogx(freq_hz, raw_loss_db_list, color='blue', linewidth=2.5, marker='o', 
                 markersize=6, label='未校准的衰减 (Uncalibrated)', linestyle='-')
    plt.semilogx(freq_hz, thru_loss_db_list, color='green', linewidth=2.5, marker='s', 
                 markersize=6, label='直通的衰减 (Thru)', linestyle='--')
    plt.semilogx(freq_hz, calibrated_loss_db_list, color='red', linewidth=2.5, marker='^', 
                 markersize=6, label='校准后的衰减 (Calibrated)', linestyle='-.')
    
    plt.title(f"滤波器衰减曲线 - 第{measurement_number}次测量 (300Hz - 20kHz)", fontsize=14, fontweight='bold')
    plt.xlabel("频率 (Hz，对数刻度)", fontsize=12)
    plt.ylabel("衰减 (dB)", fontsize=12)
    plt.ylim(-2, 0)
    
    ax = plt.gca()
    ax.xaxis.set_major_formatter(FuncFormatter(freq_formatter))
    
    plt.grid(True, which="both", linestyle="--", alpha=0.7)
    plt.legend(fontsize=11, loc='best')
    plt.tight_layout()
    
    # 保存曲线
    filename = f"Attenuation_Curves_Measurement_{measurement_number}.png"
    plt.savefig(filename, dpi=300)
    print(f" 第{measurement_number}次测量曲线已保存至: {filename}")
    plt.close()

# 首次测量
input("\n【第二步：滤波器测量】\n请断开直通，将无源滤波器接入中间（AWG->250Ω->DUT->CH2并联300Ω）。\n连接完成后，按回车键开始测量...")

awg.write("C1:OUTP ON")
measurement_count = 0

results = perform_filter_measurement(awg, osc, freq_list, v_ref_list, AWG_VOLTAGE, INITIAL_SCALE_DELAY, AUTOSET_DELAY)
measurement_count += 1
plot_and_save_measurement(results, v_ref_list, AWG_VOLTAGE, measurement_count)

# 循环测量
while True:
    awg.write("C1:OUTP OFF")
    
    response = input("\n【测量完成】是否进行下一次测量？(y/n): ").strip().lower()
    if response != 'y':
        break
    
    print("\n请更换或调整滤波器，准备好后按回车键开始下一次测量...")
    input()
    
    awg.write("C1:OUTP ON")
    results = perform_filter_measurement(awg, osc, freq_list, v_ref_list, AWG_VOLTAGE, INITIAL_SCALE_DELAY, AUTOSET_DELAY)
    measurement_count += 1
    plot_and_save_measurement(results, v_ref_list, AWG_VOLTAGE, measurement_count)

# ==================== 清理 ====================
awg.write("C1:OUTP OFF")
awg.close()
osc.close()
print("-" * 70)
print("自动化测试完毕。所有测量曲线已保存。")