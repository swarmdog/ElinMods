import re

with open('add_meteor_items.py', 'r', encoding='utf-8') as f:
    content = f.read()

# Replace explicit string values for _idRenderData, making sure we don't double replace
def repl(m):
    val = m.group(1)
    if not val.startswith('@') and val != '':
        return f'"_idRenderData": "@{val}",'
    return m.group(0)

content = re.sub(r'"_idRenderData":\s*"([^"]+)",', repl, content)

# For empty string ones, they are all charas, so default to @chara
content = re.sub(r'"_idRenderData":\s*"",', '"_idRenderData": "@chara",', content)

with open('add_meteor_items.py', 'w', encoding='utf-8') as f:
    f.write(content)
