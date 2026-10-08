"""Materialize the explicitly approved all-images v2 run; no cloud actions.

Approval belongs to the exact reviewed PNGs. Rebuilding from source must yield
the same bytes. The review proposal and v1 run remain historical evidence.
"""
from datetime import datetime, timezone
from pathlib import Path
import json
import math
import shutil

from dataset import ROOT, ROSTER, load, save, sha, build, verify
from evaluate import prepare

REV=ROOT/'revisions/official-gyokai-v2'
RUN=REV/'training-all-v2'
APPROVED_FINGERPRINT='74a44b910dacb1d11d1778ad2c39e2e1c4a2a5955b04753e7ca436634e06105b'


def main():
    proposal=load(REV/'review-manifest.json')
    if proposal['fingerprint']!=APPROVED_FINGERPRINT:
        raise ValueError('The reviewed proposal changed after user approval')
    records=proposal['images']
    if len(records)!=137:raise ValueError('Approved review expected 137 images')
    for r in records:
        if not r['reviewed_nonexplicit']:raise ValueError('Unreviewed output')
        if sha(r['output'])!=r['output_sha256']:raise ValueError('Reviewed PNG changed')
        if sha(r['source'])!=r['source_sha256']:raise ValueError('Source changed')
        if r.get('parent_source') and sha(r['parent_source'])!=r['parent_source_sha256']:
            raise ValueError('Parent source changed')
    if (RUN/'approval.json').exists():
        raise ValueError('Approved run already exists; verify/resume it instead of rebuilding')
    RUN.mkdir(parents=True,exist_ok=True)
    save(RUN/'approval.json',{
        'recorded_at':datetime.now(timezone.utc).isoformat(),
        'user_instruction':'全部纳入训练，执行训练计划',
        'proposal_fingerprint':APPROVED_FINGERPRINT,
        'scope':'All 137 reviewed nonexplicit prepared outputs, including previous holdout and reserve images',
        'independent_holdout':False,
        'allow_upload':True,'allow_submit':True,
        'existing_credits_only':True,'paid_compute_or_recharge_allowed':False,
        'pilot_gate':'Kuma and ALyCE first; others await pilot conversion and visual acceptance',
        'prior_attempts':{'kuma':1,**{r:0 for r in ROSTER if r!='kuma'}},
        'previous_cancelled_kuma_task':'183237'})
    shutil.copy2(REV/'review-manifest.json',RUN/'approved-review-manifest.json')
    save(RUN/'candidates.json',records)
    reviews=[]
    for r in records:
        reviews.append({k:r[k] for k in (
            'id','group','outfit','caption','crop','background_rgb','reviewed_nonexplicit',
            'reviewer','selection_note','limb_coverage') if k in r})
        reviews[-1].update(split='train',previous_review_split=r['split'],
                           approved_review_output_sha256=r['output_sha256'])
    save(RUN/'curation.json',reviews)
    build(RUN,'v2',require_holdout=False)
    summaries=load(RUN/'dataset-summary.json')
    approved={r['id']:r for r in records}
    for s in summaries:
        role=s['character'];m=load(RUN/'datasets'/role/'manifest.json')
        for r in m['images']:
            if r['output_sha256']!=approved[r['id']]['output_sha256']:
                raise ValueError(f'Prepared image differs from approved PNG: {r["id"]}')
        s.update(planned_steps=6000,step_policy='6000-step pilots; other eight provisional pending pilot acceptance and live quotes',
                 nominal_presentations_per_image=12000/s['train'],
                 quantity_policy='2026-10-09 explicit all-images approval; no independent holdout',
                 validation_limit='All reviewed sources are training inputs; visual acceptance uses new fixed prompts/seeds and controls, not independent held-out source scoring')
        m['summary']=s;save(RUN/'datasets'/role/'manifest.json',m);save(RUN/'datasets'/role/'summary.json',s)
        config=load(RUN/'configs'/f'{role}.json')
        config['requested_parameters'].update(maxTrainSteps=6000,maxTrainEpochs=math.ceil(6000/math.ceil(10*s['train']/2)))
        config['experiment'].update(revision='2026-10-09-approved-all-v2',checkpoint_targets=[1000,3000,6000],
            prior_cloud_jobs=1 if role=='kuma' else 0,independent_holdout=False,
            provisional_steps_until_pilot_acceptance=role not in ('kuma','alyce'))
        save(RUN/'configs'/f'{role}.json',config)
    save(RUN/'dataset-summary.json',summaries)
    shutil.copy2(ROOT/'character-features.json',RUN/'character-features.json')
    prepare(RUN);verify(RUN)
    status=load(REV/'status.json')
    status.update(phase='APPROVED_PREPARING_PILOT_SUBMISSION',training_gate='USER_APPROVED_ALL_REVIEW_IMAGES',
                  allow_upload=True,allow_submit=True,approved_run=str(RUN),
                  reason='User explicitly approved all reviewed images and execution of the training plan')
    save(REV/'status.json',status)
    save(RUN/'status.json',{'phase':'READY_FOR_PILOT_SUBMISSION','training_finished':False,
         'weights_loadable':False,'visual_acceptance':'NOT_RUN','approval':str(RUN/'approval.json')})
    print(json.dumps({'run':str(RUN),'counts':{s['character']:s['train'] for s in summaries}},ensure_ascii=False))


if __name__=='__main__':main()
