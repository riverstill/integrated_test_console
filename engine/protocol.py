"""C# <-> Python 双向管道协议.

stdout (Python -> C#): 每行一个 JSON, 统称事件:
  {"type":"log", "msg": str, "level": "info|warn|error"}
  {"type":"phase", "phase": str, "msg": str}            # 阶段切换
  {"type":"point", ...}                                # 每个频点实时数据
  {"type":"need_confirm", "phase": str, "prompt": str} # 需要用户确认(换直通/DUT)
  {"type":"round_done", "round": int, "label": str}
  {"type":"result", "out_dir": str, "files": [...]}
  {"type":"error", "msg": str}
  {"type":"done", "msg": str}

stdin (C# -> Python): 每行一个 JSON 控制命令:
  {"cmd":"pause"} | {"cmd":"resume"} | {"cmd":"stop"}
  {"cmd":"confirm", "label": "..."}   # 确认继续下一阶段/下一轮
"""
import json
import sys
import threading


def emit(obj: dict) -> None:
    sys.stdout.write(json.dumps(obj, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def log(msg: str, level: str = "info") -> None:
    emit({"type": "log", "msg": msg, "level": level})


class Control:
    """暂停/停止/确认状态. 由 stdin 监听线程写入, 测量主循环读取."""

    def __init__(self):
        self._paused = False
        self._stopped = False
        self._confirm_event = threading.Event()
        self._confirm_label = ""
        self._lock = threading.Lock()

    # --- C# 控制入口 ---
    def pause(self):
        with self._lock:
            self._paused = True

    def resume(self):
        with self._lock:
            self._paused = False

    def stop(self):
        with self._lock:
            self._stopped = True
            self._paused = False
        self._confirm_event.set()  # 避免卡在等待确认

    def confirm(self, label: str = ""):
        with self._lock:
            self._confirm_label = label
        self._confirm_event.set()

    # --- 测量循环查询 ---
    @property
    def stopped(self) -> bool:
        with self._lock:
            return self._stopped

    def wait_if_paused(self, poll_s: float = 0.1):
        """若已暂停则阻塞等待, 期间响应 stop. 由每频点前调用."""
        import time
        first = True
        while True:
            with self._lock:
                stopped, paused = self._stopped, self._paused
            if stopped:
                return
            if not paused:
                return
            if first:
                emit({"type": "phase", "phase": "paused", "msg": "已暂停,等待继续..."})
                first = False
            time.sleep(poll_s)

    def wait_confirm(self, phase: str, prompt: str, auto: bool = False,
                     auto_label: str = "") -> str:
        """阻塞等待 C# 确认. auto=True 时直接返回(用于 --auto-confirm / 自动化测试)."""
        if auto:
            return auto_label
        self._confirm_event.clear()
        with self._lock:
            self._confirm_label = ""
        emit({"type": "need_confirm", "phase": phase, "prompt": prompt})
        self._confirm_event.wait()  # stop() 也会 set, 避免死锁
        with self._lock:
            return self._confirm_label


def start_stdin_listener(ctrl: Control) -> threading.Thread:
    """后台线程监听 stdin 控制行. stdin 关闭(EOF)时自动退出."""

    def _loop():
        for line in sys.stdin:
            line = line.strip()
            if not line:
                continue
            try:
                msg = json.loads(line)
            except Exception:
                continue
            cmd = msg.get("cmd", "")
            if cmd == "pause":
                ctrl.pause()
            elif cmd == "resume":
                ctrl.resume()
            elif cmd == "stop":
                ctrl.stop()
                break
            elif cmd == "confirm":
                ctrl.confirm(str(msg.get("label", "")))

    t = threading.Thread(target=_loop, daemon=True)
    t.start()
    return t
