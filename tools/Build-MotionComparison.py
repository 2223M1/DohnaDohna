"""Review sheets from actual recordings; these are not a PASS oracle."""
import argparse
import json
import os
from pathlib import Path
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser()
parser.add_argument("original", type=Path)
parser.add_argument("host", type=Path)
parser.add_argument("deaths", type=Path)
parser.add_argument("output", type=Path)
parser.add_argument("--gallery", action="store_true")
args = parser.parse_args()
args.output.mkdir(exist_ok=True, parents=True)
roles = ["kuma", "alyce", "antena", "tora", "kikuchiyo", "medhico", "joker", "zappa", "kirakira", "porno"]
for role in roles:
    sheet = Image.new("RGB", (1920, 6*316), "#222222")
    draw = ImageDraw.Draw(sheet)
    for i, cue in enumerate(["idle", "strike", "special", "cast", "hit-and-return", "dead"]):
        y = i*316
        draw.text((8,y+3), f"{role}/{cue} ORIGINAL native controller (VFR, no damage orchestration)", fill="white")
        caption = "STS2 real death (optional female cut-in ON; default OFF)" if cue == "dead" else "STS2 production player (fixed-delta recording, ordinary attack)"
        draw.text((968,y+3), caption, fill="white")
        if cue == "hit-and-return":
            # Original fixture has independent hit and jump playback. Show both;
            # do not label it as a recorded original combat transition.
            for j, name in enumerate(["hit", "return"]):
                with Image.open(args.original/(role+"-"+name+".jpg")) as image:
                    strip = image.crop((0,0,image.width,image.height//2)).resize((960,147))
                sheet.paste(strip, (0,y+22+j*147))
        else:
            with Image.open(args.original/(role+"-"+cue+".jpg")) as image:
                sheet.paste(image.resize((960,294)), (0,y+22))
        host = args.deaths if cue == "dead" else args.host
        with Image.open(host/(role+"-"+cue+".jpg")) as image:
            sheet.paste(image.resize((960,270)), (960,y+22))
    sheet.save(args.output/(role+".jpg"),quality=94)
if args.gallery:
    records = []
    for role in roles:
        for cue in ["idle", "strike", "special", "cast", "hit", "return", "dead"]:
            host_cue = "dead-body" if cue == "dead" else cue
            source = args.original / f"{role}-{cue}.mp4"
            host = args.host / f"{role}-{host_cue}.mp4"
            if not source.is_file() or not host.is_file():
                raise ValueError(f"Missing recorded comparison: {role}/{cue}")
            records.append(dict(role=role,cue=cue,
                original=Path(os.path.relpath(source,args.output)).as_posix(),
                host=Path(os.path.relpath(host,args.output)).as_posix()))
    html = '''<!doctype html><html lang="zh"><meta charset="utf-8"><title>DohnaDohna 动作对照</title>
<style>body{background:#172027;color:#ecece5;font:16px system-ui;margin:28px}select,button{font:inherit;margin:5px;padding:8px;background:#33434c;color:white;border:1px solid #698190;border-radius:5px}.pair{display:grid;grid-template-columns:1fr 1fr;gap:18px}video{width:100%;background:black}p{line-height:1.7;max-width:1100px}small{color:#b1c5ca}</style>
<h1>原作 / STS2 后台实机动作对照</h1>
<p>左侧：原作隔离引擎、原生动作控制器，按实际时间戳采集。右侧：STS2 真实生产播放器、60Hz 固定模拟录制。没有音频验收；左右目标、构图与原生命令等待不同，播放按钮只重置两端起点，不伪造逐帧等时。普通攻击按已确认要求不带原作镜头/黑幕/敌人身体控制，命中闪光与爆炸改用 STS2 原生反馈；处决才保留原作命中表现。</p>
<label>角色 <select id="role"></select></label><label>动作 <select id="cue"></select></label><button id="play">从头并排播放</button><button id="pause">暂停</button>
<div class="pair"><section><h2>多娜多娜原生控制器</h2><video id="original" controls muted></video></section><section><h2>STS2 模组生产播放器</h2><video id="host" controls muted></video></section></div>
<p><small>原作回位为独立跳跃动作，不是完整战斗的返回距离；倒下不含原作海报调度。STS2 倒下为无遮挡的身体观察片段，真实死亡/海报另有 Presentation 录制。女性特殊死亡海报现在默认关闭，可在游戏内模组设置开启；关键帧表的海报栏演示开启状态，不代表默认效果。关键帧并排图和完整来源/限制见验证报告，不能把此浏览页当成“完全一致”结论。</small></p>
<script>const records=__DATA__;const roles=[...new Set(records.map(r=>r.role))];const names={kuma:'阿熊',alyce:'爱丽丝',antena:'安缇娜',tora:'虎太郎',kikuchiyo:'菊千代',medhico:'梅蒂可',joker:'小丑',zappa:'扎帕',kirakira:'绮菈绮菈',porno:'珀尔诺'};const cues={idle:'待机',strike:'打击',special:'特殊攻击',cast:'施放',hit:'受击',return:'回位',dead:'倒下'};const r=document.getElementById('role'),c=document.getElementById('cue'),a=document.getElementById('original'),b=document.getElementById('host');for(const k of roles)r.add(new Option(names[k],k));for(const [k,v]of Object.entries(cues))c.add(new Option(v,k));function update(){const x=records.find(x=>x.role===r.value&&x.cue===c.value);a.src=x.original;b.src=x.host}r.onchange=c.onchange=update;document.getElementById('play').onclick=()=>{a.currentTime=b.currentTime=0;a.play();b.play()};document.getElementById('pause').onclick=()=>{a.pause();b.pause()};update();</script></html>'''
    (args.output/"index.html").write_text(html.replace("__DATA__",json.dumps(records,ensure_ascii=False)),encoding="utf-8")
print("Created actual-recording review sheets; manual conclusions required")
