#!/usr/bin/env python3
"""
뜰채 IR LED 추적기 → Unity "뜰채 바다"로 UDP 전송

천장 카메라(850nm 패스 필터 장착)로 뜰채 중앙의 IR LED를 찾고,
LED 개수(1~4개)로 플레이어를 구분해 투사 화면 좌표(0~1)로 보냅니다.

사용법
  1) 보정 (설치 후 한 번):  python ir_tracker.py --calibrate
     - Unity 게임에서 F2를 눌러 둥근 화면 안의 보정 마커(1~4)를 띄웁니다.
     - 아무 뜰채나 마커 1 위에 올리고 스페이스, 2·3·4도 같은 순서로 반복합니다.
     - calib.json이 저장됩니다.
  2) 실행:  python ir_tracker.py --show
     - 창에 보이는 점 위에 P1~P4가 붙으면 정상입니다. q로 종료.

메시지 형식: "1 0.5230 0.3310\n2 0.1200 0.8800\n" (플레이어 번호, 가로, 세로 / 원점 왼쪽 위)
"""
import argparse
import collections
import json
import os
import socket
import sys
import time

import cv2
import numpy as np

import math


def marker_viewports(aspect):
    """Unity FishPondGame.MarkerViewports() 와 같은 식.
    둥근 화면 안 대각선 4곳 (왼쪽 위 → 오른쪽 위 → 오른쪽 아래 → 왼쪽 아래), 원점 왼쪽 위."""
    m = min(aspect, 1.0)
    r = 0.735 * m
    out = []
    for deg in (135, 45, 315, 225):
        a = math.radians(deg)
        out.append((0.5 + r * math.cos(a) / (2 * aspect), 0.5 - r * math.sin(a) / 2))
    return out
COLORS = [(90, 107, 255), (63, 210, 255), (151, 220, 61), (255, 169, 90)]  # BGR, P1~P4


def open_camera(args):
    backend = cv2.CAP_DSHOW if os.name == "nt" else cv2.CAP_ANY
    cap = cv2.VideoCapture(args.camera, backend)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, args.width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, args.height)
    cap.set(cv2.CAP_PROP_FPS, args.fps)
    if args.exposure is not None:
        cap.set(cv2.CAP_PROP_AUTO_EXPOSURE, 0.25)  # 수동 노출 (DirectShow 기준)
        cap.set(cv2.CAP_PROP_EXPOSURE, args.exposure)
    if not cap.isOpened():
        sys.exit("카메라 %d 번을 열 수 없습니다. --camera 번호를 바꿔 보세요." % args.camera)
    return cap


def find_blobs(gray, thresh, min_area):
    _, bw = cv2.threshold(gray, thresh, 255, cv2.THRESH_BINARY)
    bw = cv2.morphologyEx(bw, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    n, _, stats, cents = cv2.connectedComponentsWithStats(bw)
    blobs = []
    for i in range(1, n):
        area = stats[i, cv2.CC_STAT_AREA]
        if area >= min_area:
            blobs.append((float(cents[i][0]), float(cents[i][1]), int(area)))
    return blobs


def cluster(blobs, radius):
    """가까운 LED 점끼리 묶어 뜰채 하나로 만듭니다."""
    used = [False] * len(blobs)
    out = []
    r2 = radius * radius
    for i in range(len(blobs)):
        if used[i]:
            continue
        group, used[i], k = [i], True, 0
        while k < len(group):
            ax, ay, _ = blobs[group[k]]
            for j in range(len(blobs)):
                if not used[j] and (blobs[j][0] - ax) ** 2 + (blobs[j][1] - ay) ** 2 <= r2:
                    used[j] = True
                    group.append(j)
            k += 1
        pts = [blobs[g] for g in group]
        out.append({
            "x": sum(p[0] for p in pts) / len(pts),
            "y": sum(p[1] for p in pts) / len(pts),
            "n": len(pts),
            "area": sum(p[2] for p in pts),
        })
    return out


class Track:
    """LED 개수가 잠깐 잘못 보여도 플레이어 번호가 흔들리지 않도록 최근 8프레임 다수결."""

    def __init__(self, x, y, n, now):
        self.x, self.y, self.last = x, y, now
        self.votes = collections.deque(maxlen=8)
        self.votes.append(n)

    @property
    def player(self):
        counts = collections.Counter(v for v in self.votes if 1 <= v <= 4)
        return counts.most_common(1)[0][0] if counts else 0


def to_viewport(H, frame_size, x, y):
    if H is None:
        w, h = frame_size
        return x / w, y / h
    p = cv2.perspectiveTransform(np.array([[[x, y]]], dtype=np.float32), H)[0][0]
    return float(p[0]), float(p[1])


def load_calib(path):
    if not os.path.exists(path):
        print("[경고] %s 가 없습니다. --calibrate 로 보정하기 전까지는 카메라 화면 비율 그대로 보냅니다." % path)
        return None
    with open(path, encoding="utf-8") as f:
        return np.array(json.load(f)["H"], dtype=np.float32)


def calibrate(args):
    cap = open_camera(args)
    src = []
    print("Unity에서 F2로 마커를 띄우고, 뜰채를 마커 1 위에 올린 뒤 스페이스를 누르세요. (q: 취소)")
    while len(src) < 4:
        ok, frame = cap.read()
        if not ok:
            continue
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        groups = cluster(find_blobs(gray, args.threshold, args.min_area), args.cluster)
        view = frame.copy()
        for g in groups:
            cv2.circle(view, (int(g["x"]), int(g["y"])), 12, (0, 255, 255), 2)
        for i, p in enumerate(src):
            cv2.drawMarker(view, (int(p[0]), int(p[1])), (0, 255, 0), cv2.MARKER_CROSS, 24, 2)
            cv2.putText(view, str(i + 1), (int(p[0]) + 8, int(p[1]) - 8), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (0, 255, 0), 2)
        cv2.putText(view, "Marker %d: place a net, press SPACE" % (len(src) + 1), (10, 30),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2)
        cv2.imshow("calibrate", view)
        key = cv2.waitKey(1) & 0xFF
        if key == ord("q"):
            return
        if key == ord(" "):
            if not groups:
                print("  LED가 안 보입니다. 뜰채 스위치와 --threshold 값을 확인하세요.")
                continue
            g = max(groups, key=lambda c: c["area"])
            src.append((g["x"], g["y"]))
            print("  마커 %d 저장: (%.1f, %.1f)" % (len(src), g["x"], g["y"]))
    markers = marker_viewports(args.aspect)
    H = cv2.getPerspectiveTransform(np.array(src, dtype=np.float32), np.array(markers, dtype=np.float32))
    with open(args.calib, "w", encoding="utf-8") as f:
        json.dump({"H": H.tolist(), "camera_points": src, "markers": markers, "aspect": args.aspect}, f, indent=2)
    print("보정 완료 → %s" % args.calib)
    cap.release()
    cv2.destroyAllWindows()


def run(args):
    H = load_calib(args.calib)
    cap = open_camera(args)
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)
    tracks = []
    smooth = {}
    frames, t0 = 0, time.time()
    print("전송 중 → %s:%d  (q: 종료)" % target)

    while True:
        ok, frame = cap.read()
        if not ok:
            continue
        now = time.time()
        h, w = frame.shape[:2]
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        groups = cluster(find_blobs(gray, args.threshold, args.min_area), args.cluster)

        # 이전 프레임의 트랙과 가장 가까운 묶음을 이어붙임
        free = list(range(len(groups)))
        for tr in tracks:
            best, bd = None, args.match ** 2
            for gi in free:
                d = (groups[gi]["x"] - tr.x) ** 2 + (groups[gi]["y"] - tr.y) ** 2
                if d < bd:
                    best, bd = gi, d
            if best is not None:
                g = groups[best]
                tr.x, tr.y, tr.last = g["x"], g["y"], now
                tr.votes.append(g["n"])
                free.remove(best)
        for gi in free:
            g = groups[gi]
            tracks.append(Track(g["x"], g["y"], g["n"], now))
        tracks = [t for t in tracks if now - t.last < 0.3]

        # 플레이어별로 가장 최근 트랙 하나씩
        chosen = {}
        for tr in tracks:
            p = tr.player
            if p and (p not in chosen or tr.last > chosen[p].last):
                chosen[p] = tr

        lines = []
        for p, tr in sorted(chosen.items()):
            vx, vy = to_viewport(H, (w, h), tr.x, tr.y)
            if p in smooth:
                sx, sy = smooth[p]
                vx, vy = sx + (vx - sx) * args.smooth, sy + (vy - sy) * args.smooth
            smooth[p] = (vx, vy)
            lines.append("%d %.4f %.4f" % (p, vx, vy))
        for p in list(smooth):
            if p not in chosen:
                del smooth[p]
        if lines:
            sock.sendto(("\n".join(lines) + "\n").encode("utf-8"), target)

        frames += 1
        if args.show:
            view = frame.copy()
            for p, tr in chosen.items():
                c = COLORS[p - 1]
                cv2.circle(view, (int(tr.x), int(tr.y)), 18, c, 2)
                cv2.putText(view, "P%d" % p, (int(tr.x) + 20, int(tr.y)), cv2.FONT_HERSHEY_SIMPLEX, 0.8, c, 2)
            fps = frames / max(1e-3, now - t0)
            cv2.putText(view, "%.0f fps  nets:%d" % (fps, len(chosen)), (10, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (255, 255, 255), 2)
            cv2.imshow("tracker", view)
            if cv2.waitKey(1) & 0xFF == ord("q"):
                break

    cap.release()
    cv2.destroyAllWindows()


def main():
    ap = argparse.ArgumentParser(description="뜰채 IR LED 추적기")
    ap.add_argument("--camera", type=int, default=0, help="카메라 번호 (기본 0)")
    ap.add_argument("--width", type=int, default=1280)
    ap.add_argument("--height", type=int, default=720)
    ap.add_argument("--fps", type=int, default=60)
    ap.add_argument("--exposure", type=float, default=None, help="수동 노출 (Windows 예: -7). 낮을수록 어둡게")
    ap.add_argument("--threshold", type=int, default=200, help="LED로 볼 밝기 (0~255)")
    ap.add_argument("--min-area", type=int, default=4, help="LED 점 최소 픽셀 수")
    ap.add_argument("--cluster", type=float, default=45, help="같은 뜰채로 묶을 LED 간 거리 (픽셀)")
    ap.add_argument("--match", type=float, default=120, help="프레임 사이 같은 뜰채로 이어볼 거리 (픽셀)")
    ap.add_argument("--smooth", type=float, default=0.6, help="떨림 보정 0~1 (1 = 보정 없음)")
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=7777)
    ap.add_argument("--calib", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "calib.json"))
    ap.add_argument("--aspect", type=float, default=16 / 9, help="Unity 게임 화면 가로/세로 비율 (기본 16:9)")
    ap.add_argument("--calibrate", action="store_true", help="보정 모드 (둥근 화면 안 마커 4개)")
    ap.add_argument("--show", action="store_true", help="카메라 화면 보기")
    args = ap.parse_args()
    if args.calibrate:
        calibrate(args)
    else:
        run(args)


if __name__ == "__main__":
    main()
