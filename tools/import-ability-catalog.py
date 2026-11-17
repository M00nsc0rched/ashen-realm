#!/usr/bin/env python3
"""Read the catalog without Excel and regenerate the typed Unity table.

Usage: python tools/import-ability-catalog.py path/to/KATALOGUS.xlsx
Also accepts the read-only ability-catalog.json extraction. No source file is edited.
"""
import argparse
import hashlib
import json
import re
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CLASS = {'knight':'Knight','rogue':'Gravethief','mage':'Witch','warden':'Warden','necromancer':'Necromancer','alchemist':'Alchemist','common':'Knight','follower':'Gravethief'}
KIND = {'physical':'Physical','magic':'Magic','defense':'Defense','buff':'Buff','heal':'Heal','charm':'Charm'}
LEGACY = {'shieldbash':'ShieldBash','rally':'Rally','backstab':'Backstab','poisonblade':'PoisonBlade','drain':'LifeDrain','hex':'Hex','smite':'Smite','heal':'Mend','n_bonespear':'BoneSpear','n_bonearmor':'BoneArmor','flask_vitriol':'AcidFlask','elixir_mend':'HealingBrew','whirlwind':'Whirlwind','ironskin':'IronSkin','fanofknives':'FanOfKnives','caltrops':'Caltrops','frostlance':'FrostLance','chainlightning':'ChainLightning','sanctuary':'Sanctuary','consecration':'Consecration','n_poisonnova':'BoneNova','n_bloodstar':'SoulHarvest','flask_pyre':'FireBomb','vial_torpor':'FlashPowder'}
OLD_EFFECTS = ['ShieldBash','Rally','Backstab','PoisonBlade','LifeDrain','Hex','Smite','Mend','BoneSpear','BoneArmor','AcidFlask','HealingBrew','Whirlwind','IronSkin','FanOfKnives','Caltrops','FrostLance','ChainLightning','Sanctuary','Consecration','BoneNova','SoulHarvest','FireBomb','FlashPowder']
FIELD = {'power':'Power','hits':'Hits','accuracyPercent':'Accuracy','criticalChancePercent':'CriticalChance','stunChancePercent':'StunChance','lifestealPercent':'Lifesteal','executeAtHpPercent':'ExecuteThreshold','poisonPerTurn':'PoisonPerTurn','poisonTurns':'PoisonTurns','attackReduction':'AttackReduction','attackReductionTurns':'AttackReductionTurns','shieldDefMultiplier':'ShieldDefMultiplier','regenerationPerTurn':'RegenerationPerTurn','regenerationTurns':'RegenerationTurns','attackBonus':'AttackBonus','attackBonusTurns':'AttackBonusTurns','healFlat':'HealFlat','healMagMultiplier':'HealMagMultiplier','selfDamage':'SelfDamage','restoreSp':'RestoreFocus','convertChancePercent':'ConvertChance','stealEnemyAttackMagicPercent':'StealFraction','stealBossAttackMagicPercent':'StealBossFraction','stealTurns':'StealTurns','holy':'Holy','cleansePoison':'CleansePoison'}
FRACTIONS = {k for k in FIELD if k.endswith('Percent')}
INT_FIELDS = {'Hits','PoisonTurns','AttackReductionTurns','RegenerationTurns','AttackBonusTurns','StealTurns'}
TRANSLATE = str.maketrans({'—':'-','–':'-','−':'-','×':'x','≤':'<=','≥':'>=','„':'"','”':'"','“':'"','’':"'",'…':'...'})

def text(value): return (value or '').translate(TRANSLATE)
def quoted(value): return json.dumps(text(value), ensure_ascii=False)
def number(value): return f'{float(value):.6g}f'

def parse_effect(s):
    out={}
    for k,p in {'power':r'erő (\d+)','hits':r'(\d+)× erő','accuracyPercent':r'pontosság (\d+)%','stunChancePercent':r'kábítás (\d+)%','criticalChancePercent':r'krit (\d+)%','lifestealPercent':r'életlopás (\d+)%','executeAtHpPercent':r'kivégzés ≤(\d+)%','shieldDefMultiplier':r'pajzs DEF×(\d+)','selfDamage':r'önsebzés (\d+)','restoreSp':r'\+(\d+) SP','convertChancePercent':r'megbűvölés esélye (\d+)%'}.items():
        m=re.search(p,s)
        if m:out[k]=int(m[1])
    for a,b,p in [('poisonPerTurn','poisonTurns',r'méreg (\d+)/kör × (\d+) kör'),('regenerationPerTurn','regenerationTurns',r'regeneráció (\d+)/kör × (\d+) kör'),('attackBonus','attackBonusTurns',r'\+(\d+) ATK × (\d+) kör'),('attackReduction','attackReductionTurns',r'gyengítés −(\d+) ATK × (\d+) kör'),('healFlat','healMagMultiplier',r'gyógyít (\d+) \+ MAG×(\d+)')]:
        m=re.search(p,s)
        if m:out[a],out[b]=int(m[1]),int(m[2])
    if 'szent' in s:out['holy']=True
    if 'méreg eltávolítása' in s:out['cleansePoison']=True
    if 'pajzs "def"' in s:out['shieldLiteral']='def'
    if 'elveszi az ellenfél ATK/MAG-jának' in s:out.update(stealEnemyAttackMagicPercent=50,stealBossAttackMagicPercent=5,stealTurns=3)
    return out

def read_workbook(path):
    ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
    with zipfile.ZipFile(path) as z:
        shared=[''.join(e.itertext()) for e in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si',ns)]
        rels={e.attrib['Id']:e.attrib['Target'] for e in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        tables={}
        for sheet in ET.fromstring(z.read('xl/workbook.xml')).findall('s:sheets/s:sheet',ns):
            target=rels[sheet.attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']]
            rows=[]
            for row in ET.fromstring(z.read(target.lstrip('/') if target.startswith('/') else 'xl/'+target)).findall('s:sheetData/s:row',ns):
                values={}
                for cell in row.findall('s:c',ns):
                    v=cell.find('s:v',ns); v=v.text if v is not None else ''
                    if cell.attrib.get('t')=='s':v=shared[int(v)] if v else ''
                    elif cell.attrib.get('t')=='inlineStr':v=''.join(cell.find('s:is',ns).itertext())
                    values[re.sub(r'\d+$','',cell.attrib['r'])]=v
                if values:rows.append((int(row.attrib['r']),values))
            tables[sheet.attrib['name']]=rows
    classes=[]
    for r,v in tables['Kasztok'][1:]:
        classes.append({'values':{'Kaszt':v['A'],'Kód':v['C'],'Passzív':v['M'],'Passzív leírása':v['O']}})
    owners={c['values']['Kaszt']:c['values']['Kód'] for c in classes}
    types={'fizikai':'physical','mágia':'magic','védekezés':'defense','erősítés':'buff','gyógyítás':'heal','bűbáj':'charm'}
    abilities=[]
    for r,v in tables['Képességek'][1:]:
        tiers=[{'text':v.get(c,''),'effects':parse_effect(v.get(c,''))} for c in 'MNO' if v.get(c)]
        abilities.append({'id':v['D'],'classId':owners.get(v['A'],'common' if v['A']=='Közös' else 'follower'),'name':v['B'],'englishName':v.get('C'),'type':types[v['E']],'spCost':int(v['F']),'path':v.get('G'),'signature':v.get('H')=='igen','description':v.get('I'),'tiers':tiers,'source':f"'Képességek'!A{r}:O{r}"})
    recipes=[]
    for r,v in tables['Alkimista receptek'][1:]:
        recipes.append({'values':dict(zip(['Recept','Angol név','Kód','Kategória','Növény','INT-küszöb','Leírás'],[v.get(c) for c in 'ABCDEFG'])),'source':f"'Alkimista receptek'!A{r}:G{r}"})
    return {'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'abilities':abilities,'classes':classes,'recipes':recipes}

def effect_name(a):
    if a['id'] in LEGACY:return LEGACY[a['id']]
    if a['id']=='bonespear':return 'WitchBoneSpear'
    name=''.join(w[:1].upper()+w[1:] for w in re.findall(r'[A-Za-z0-9]+',a.get('englishName') or a['id']))
    return ('Necromancer' if a['id'].startswith('n_') else '')+name

DELIVERY = {
    'whirlwind':'Nova','seismicslam':'Nova','leapslam':'Leap','furiouscharge':'Charge','shieldcharge':'Charge',
    'fanofknives':'Fan','bladefury':'Nova','deathblossom':'Fan','caltrops':'Field',
    'firebolt':'Bolt','frostlance':'Bolt','frozenorb':'Orb','chainlightning':'Chain','blizzard':'Field','inferno':'Field','bonespear':'Bolt','corpseexplosion':'Nova','teeth':'Fan','staticfield':'Nova','glacialspike':'Bolt','firewall':'Field',
    'smite':'Bolt','holybolt':'Bolt','blessedhammer':'Orb','fistoftheheavens':'Target','consecration':'Field','lastrites':'Target',
    'n_bonespear':'Bolt','n_teeth':'Fan','n_poisonnova':'Nova','n_corpseburst':'Nova','n_bonespirit':'Bolt','n_bloodstar':'Bolt',
    'starter_tonic':'Bolt','flask_vitriol':'Bolt','flask_pyre':'Nova','vial_miasma':'Field','vial_torpor':'Target','vial_solvent':'Target','flask_shatter':'Fan'}
COOLDOWN={'Melee':8,'Bolt':8,'Fan':10,'Chain':12,'Nova':12,'Field':14,'Charge':10,'Leap':10,'Self':12,'Target':10,'Orb':12}
REACH={'Melee':(3,2.4),'Bolt':(16,1),'Fan':(13,4),'Chain':(14,5),'Nova':(8,4.5),'Field':(12,4),'Charge':(8,2.5),'Leap':(8,3.5),'Self':(0,0),'Target':(14,1),'Orb':(14,3)}

def recipe_tiers(id):
    result=[]
    for t in range(3):
        n=t+1
        effects={
            'starter_tonic':dict(power=10,healFlat=4),
            'flask_vitriol':dict(power=16+4*t,poisonPerTurn=4+2*t,poisonTurns=3,attackReduction=3+t,attackReductionTurns=3),
            'tonic_ward':dict(shieldDefMultiplier=2+t),
            'nourishing_stew':dict(healFlat=12+6*t,regenerationPerTurn=2+t,regenerationTurns=3),
            'flask_pyre':dict(power=22+6*t,poisonPerTurn=5+2*t,poisonTurns=3),
            'vial_miasma':dict(power=4+2*t,poisonPerTurn=6+2*t,poisonTurns=4,attackReduction=4+2*t,attackReductionTurns=3),
            'brew_vigor':dict(attackBonus=6+3*t,attackBonusTurns=3,shieldDefMultiplier=2+t),
            'elixir_mend':dict(healFlat=20+8*t,regenerationPerTurn=4+2*t,regenerationTurns=3),
            'vial_torpor':dict(power=2+2*t,stunChancePercent=100,attackReduction=3+2*t,attackReductionTurns=2),
            'vial_solvent':dict(power=8+2*t,attackReduction=10+4*t,attackReductionTurns=3),
            'flask_shatter':dict(power=8+2*t,hits=4+t,accuracyPercent=85,criticalChancePercent=15),
            'draught_fervor':dict(attackBonus=10+4*t,attackBonusTurns=3,regenerationPerTurn=2+t,regenerationTurns=3),
            'cordial_marrow':dict(healFlat=28+8*t,regenerationPerTurn=5+2*t,regenerationTurns=4),
        }[id]
        parts=[]
        if 'power' in effects:parts.append((f"{effects['hits']}x " if 'hits' in effects else '')+f"erő {effects['power']}")
        if 'healFlat' in effects:parts.append(f"gyógyít {effects['healFlat']}")
        if 'accuracyPercent' in effects:parts.append(f"pontosság {effects['accuracyPercent']}%")
        if 'criticalChancePercent' in effects:parts.append(f"krit {effects['criticalChancePercent']}%")
        if 'stunChancePercent' in effects:parts.append(f"kábítás {effects['stunChancePercent']}%")
        if 'poisonPerTurn' in effects:parts.append(f"méreg {effects['poisonPerTurn']}/kör x {effects['poisonTurns']} kör")
        if 'attackReduction' in effects:parts.append(f"gyengítés -{effects['attackReduction']} ATK x {effects['attackReductionTurns']} kör")
        if 'shieldDefMultiplier' in effects:parts.append(f"pajzs DEFx{effects['shieldDefMultiplier']}")
        if 'regenerationPerTurn' in effects:parts.append(f"regeneráció {effects['regenerationPerTurn']}/kör x {effects['regenerationTurns']} kör")
        if 'attackBonus' in effects:parts.append(f"+{effects['attackBonus']} ATK x {effects['attackBonusTurns']} kör")
        result.append({'text':', '.join(parts), 'effects':effects})
    return result

def tier_cs(t):
    assignments=[]
    for k,v in (t.get('effects') or {}).items():
        if k=='shieldLiteral':k,v='shieldDefMultiplier',1
        if k not in FIELD:raise ValueError('Unknown effect '+k)
        field=FIELD[k]
        value=str(v).lower() if isinstance(v,bool) else str(int(v)) if field in INT_FIELDS else number(float(v)/100 if k in FRACTIONS else v)
        assignments.append(f'{field} = {value}')
    assignments.append('Summary = '+quoted(t['text']))
    return 'new AbilityTierDefinition { '+', '.join(assignments)+' }'

def generate(data):
    abilities=list(data['abilities'])
    assert len(abilities)==98 and len({a['id'] for a in abilities})==98
    for record in data['recipes']:
        v=record['values'];id=v['Kód'];kind='heal' if v['Kategória']=='étel' or id=='elixir_mend' else 'buff' if v['Kategória']=='erősítő' else 'physical' if id=='flask_shatter' else 'magic'
        abilities.append(dict(id=id,classId='alchemist',name=v['Recept'],englishName=v['Angol név'],type=kind,spCost=0,path=None,signature=True,description=v['Leírás'],tiers=recipe_tiers(id),source=record['source'],recipe=True,plantCost=int(v['Növény']),intellect=int(v['INT-küszöb'])))
    effects=[effect_name(a) for a in abilities]
    assert len(set(effects))==111, 'Effect names must be unique per record'
    enum=OLD_EFFECTS+[e for e in effects if e not in OLD_EFFECTS]
    types=ROOT/'Assets/Scripts/Story/StoryTypes.cs'
    original=types.read_text(encoding='utf-8-sig')
    enum_text='    public enum AbilityEffect\n    {\n'+''.join('        '+e+' = '+str(i)+',\n' for i,e in enumerate(enum))+'    }'
    updated=re.sub(r'    public enum AbilityEffect\s*\{[^}]+\}',enum_text,original,count=1)
    if updated==original and 'Strike = 24' not in updated:raise ValueError('Cannot locate AbilityEffect')
    types.write_text(updated,encoding='utf-8')
    lines=['// Generated by tools/import-ability-catalog.py. Edit the importer, then regenerate.',f'// Workbook SHA256: {data["sha256"]}','namespace AshenRealm.Story','{','    public static class WorkbookAbilityData','    {',f'        public const string SourceSha256 = "{data["sha256"]}";']
    for label,key in [('PassiveNames','Passzív'),('PassiveDescriptions','Passzív leírása')]:
        lines.append('        public static readonly string[] '+label+' = { '+', '.join(quoted(c['values'][key]) for c in data['classes'])+' };')
    lines.extend(['        public static AbilityDefinition[] Create() => new AbilityDefinition[]','        {'])
    mapping=[]
    for a in abilities:
        id=a['id'];kind=KIND[a['type']];delivery=DELIVERY.get(id,'Self' if kind in ('Defense','Buff','Heal') else 'Melee' if kind=='Physical' else 'Target')
        cooldown=COOLDOWN[delivery]+(2 if a['spCost']>=9 else 0)
        cooldown=min(16,cooldown);reach,radius=REACH[delivery]
        path='Attack' if a['path']=='támadó' else 'Defense' if a['path']=='védő' else 'None'
        fields=[f'Id = {quoted(id)}',f'Effect = AbilityEffect.{effect_name(a)}',f'Owner = CharacterClass.{CLASS[a["classId"]]}',f'Name = {quoted(a["name"])}',f'Description = {quoted(a["description"])}',f'Source = {quoted(a["source"])}',f'Kind = AbilityKind.{kind}',f'Delivery = AbilityDelivery.{delivery}',f'Cost = {a["spCost"]}',f'Cooldown = {number(cooldown)}',f'Range = {number(reach)}',f'Radius = {number(radius)}']
        for f,flag in [('Common',a['classId']=='common'),('Hidden',a['classId']=='follower'),('Signature',a['signature']),('IsRecipe',a.get('recipe',False))]:
            if flag:fields.append(f+' = true')
        if path!='None':fields.append('Path = NecromancerPath.'+path)
        if a.get('recipe'):fields.extend([f'PlantCost = {a["plantCost"]}',f'IntellectRequired = {a["intellect"]}'])
        lines.extend(['            new AbilityDefinition','            {','                '+', '.join(fields)+',','                Tiers = new AbilityTierDefinition[]','                {'])
        for t in a['tiers']:
            if t['text']:lines.append('                    '+tier_cs(t)+',')
        lines.extend(['                }','            },'])
        mapping.append(dict(id=id,effect=effect_name(a),kind=kind,delivery=delivery,owner=CLASS[a['classId']],source=a['source']))
    lines.extend(['        };','    }','}'])
    (ROOT/'Assets/Scripts/Story/WorkbookAbilityData.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    mapfile=ROOT/'Logs/catalog_extract/runtime-mapping.json';mapfile.parent.mkdir(parents=True,exist_ok=True)
    mapfile.write_text(json.dumps(mapping,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Generated {len(abilities)} definitions; {len(enum)} stable unique effects. SHA256 {data["sha256"]}')

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source',type=Path)
    args=parser.parse_args()
    data=json.loads(args.source.read_text(encoding='utf-8')) if args.source.suffix.lower()=='.json' else read_workbook(args.source)
    generate(data)
