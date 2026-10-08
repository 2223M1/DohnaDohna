"""Render the actual approved training inputs and last observed task status."""
import html
import shutil
from datetime import datetime, timezone
from pathlib import Path
from PIL import Image
from dataset import load, save, sha, ROSTER
from prepare_approved_v2 import REV, RUN


def main():
    out=REV/'review';summaries=load(RUN/'dataset-summary.json')
    rows=[];cards=[];records=[]
    for s in summaries:
        role=s['character'];c=load(RUN/'configs'/f'{role}.json');task=c['submission']
        state=task['status']
        label={'NOT_SUBMITTED':'训练包就绪，等待先导验收','PREPARING_DATASET':'云端准备数据集',
               'QUEUED':'排队中','TRAINING':'训练中','COMPLETED':'训练完成，等待验收',
               'FAILED':'云端失败','CANCELLED':'已取消'}.get(state,state)
        if task.get('last_observed_step') is not None:
            label+=f" · {task['last_observed_step']}/{c['requested_parameters']['maxTrainSteps']}"
        link=f'<a href="{html.escape(task["url"],quote=True)}" target="_blank">{task["task_id"]}</a>' if task.get('url') else '—'
        rows.append(f'<tr><td>{ROSTER[role]}</td><td>{s["train"]}</td><td>{label}</td><td>{link}</td><td>{c["requested_parameters"]["maxTrainSteps"]}</td></tr>')
        for r in load(RUN/'datasets'/role/'manifest.json')['images']:
            preview=out/'images'/role/(r['id']+'.png')
            if not preview.exists() or sha(preview)!=r['output_sha256']:
                preview=out/'training-images'/role/(r['id']+'.png');preview.parent.mkdir(parents=True,exist_ok=True)
                shutil.copy2(r['output'],preview)
            thumb=out/'training-thumbs'/role/(r['id']+'.jpg');thumb.parent.mkdir(parents=True,exist_ok=True)
            with Image.open(r['output']) as im:
                im.thumbnail((500,560));im.save(thumb,quality=91)
            e=html.escape
            r['selection_note']=r.get('selection_note','').replace('等待用户审阅确认','已获用户确认')
            cards.append(f'<article data-role="{role}"><a href="{preview.relative_to(out).as_posix()}" target="_blank"><img loading="lazy" src="{thumb.relative_to(out).as_posix()}" alt="{e(r["id"])}"></a><div><h3>{ROSTER[role]}</h3><p>{e(r["id"])} · {r["output_size"][0]}×{r["output_size"][1]}</p><p>{e(r.get("selection_note",""))}</p><details><summary>训练描述与来源组</summary><p>{e(r["caption"])}</p><p>{e(r["group"])}</p><code>{r["output_sha256"]}</code></details></div></article>')
            records.append(r)
    status=load(RUN/'status.json')
    state={'last_observed':status,'roles':[{**s,'submission':load(RUN/'configs'/f'{s["character"]}.json')['submission']} for s in summaries]}
    save(out/'training-status.json',state)
    template='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>多娜多娜 · Krea2 训练进度与最终输入</title><style>
*{box-sizing:border-box}body{margin:0;background:#f3f5f8;color:#182133;font:15px "Microsoft YaHei",sans-serif}header{padding:30px 4vw;background:#17243b;color:white}header p{line-height:1.8;max-width:1150px}main{padding:25px 4vw}a{color:#146989}header a{color:#9de6d1}.panel{background:white;border:1px solid #d8e0e9;border-radius:12px;padding:20px;margin-bottom:24px;overflow:auto}table{width:100%;border-collapse:collapse;white-space:nowrap}td,th{padding:11px;border-bottom:1px solid #ddd;text-align:left}h1{font-size:26px}h2{font-size:21px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(265px,1fr));gap:18px}article{background:white;border:1px solid #d8e0e9;border-radius:10px;overflow:hidden}article>a{height:360px;display:block;background:#e9edf1;padding:10px}img{width:100%;height:100%;object-fit:contain}article>div{padding:16px}article p,details{font-size:12px;line-height:1.7;overflow-wrap:anywhere}code{font-size:10px}select{padding:9px;font:inherit}nav{display:flex;gap:16px;align-items:center;position:sticky;top:0;background:#f3f5f8;padding:12px 0;z-index:2}[hidden]{display:none!important}.muted{color:#59687a;font-size:13px}
</style><header><h1>十人 Krea2 · 已批准并开始执行</h1><p>137 张最终训练输入；头部裁片已撤出，三张珀尔诺素材改用对应完整原图。包装保留完整正面，五张按145:210校正透视，绮菈绮菈使用画师平面图。</p><p>阿熊、爱丽丝先导已提交，各6000步、24魔粒；首批两人合计扣48，最近核实余额__BALANCE__魔粒。训练完成、转换可加载和视觉验收分别记录；成品以验收收据为准。后续任务步数按先导结果和实时报价确定。</p><p>已安排每30分钟自动跟进云端进度与后续转换验收。任务状态是最近观察记录，并非网页实时查询。<a href="index.html">查看历史选图提案</a></p></header><main><section class="panel"><h2>训练任务</h2><p class="muted">最近状态记录：__OBSERVED__</p><table><thead><tr><th>角色</th><th>训练图</th><th>状态</th><th>魔搭任务</th><th>计划步数</th></tr></thead><tbody>__ROWS__</tbody></table><p>全部图片用于训练，原来的验证图不再构成独立留出集。验收采用新提示词、固定种子、无LoRA及无触发词对照；特别记录手指、手腕、关节、鞋足、动作响应与训练图复刻。</p><p><a href="training-status.json">任务与训练包记录</a></p></section><h2>实际训练输入</h2><nav><select id="role" aria-label="角色筛选"><option value="all">全部十人</option>__OPTIONS__</select><span id="count"></span></nav><div class="grid">__CARDS__</div></main><script>const cards=[...document.querySelectorAll('article')],role=document.querySelector('#role');function filter(){let n=0;for(const c of cards){c.hidden=role.value!=='all'&&role.value!==c.dataset.role;if(!c.hidden)n++}document.querySelector('#count').textContent=n+' 张训练图'}role.addEventListener('change',filter);filter();</script></html>'''
    for k,v in {'__OBSERVED__':html.escape(status['observed_at']),'__BALANCE__':str(status.get('balance_last_observed','未记录')),'__ROWS__':''.join(rows),
                '__OPTIONS__':''.join(f'<option value="{r}">{n}</option>' for r,n in ROSTER.items()),'__CARDS__':''.join(cards)}.items():template=template.replace(k,v)
    (out/'training-status.html').write_text(template,encoding='utf-8')
    index=out/'index.html';page=index.read_text(encoding='utf-8')
    if 'id="execution-notice"' not in page:
        archive=REV/'archive/before-training-launch';archive.mkdir(parents=True,exist_ok=True);shutil.copy2(index,archive/'index.html')
        page=page.replace('<header>','<header><p id="execution-notice"><strong>此页为历史选图提案。用户已批准全部训练，并要求头部裁片改用完整原图。</strong> <a href="training-status.html">查看实际训练输入与当前任务 →</a></p>',1)
        page=page.replace('等待你审阅；未上传、未开始新版训练','历史审阅快照 · 当前执行进度见上方链接')
        index.write_text(page,encoding='utf-8')
    print(out/'training-status.html')


if __name__=='__main__':main()
