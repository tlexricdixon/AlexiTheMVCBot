# Aneed Static System — eight fictional AI DJs

The `DjProfiles` registry defines eight original characters, each with a short bio, a single-DJ personality prompt, and a matching portrait path under `wwwroot/img/djs`.

| DJ | Role | Mood |
| --- | --- | --- |
| Kathleen | The Midnight Storyteller | Intimate, thoughtful, restrained |
| Lexi Static | The Smartass Signal | Quick, sharp, friendly |
| Vesper Thorn | The Velvet Strategist | Elegant, precise, dry |
| Roxy Riot | The Chaos Engine | Punk, mischievous, warm |
| Mara Vale | The Redline Rebel | Passionate, defiant, cathartic |
| Sunny Knox | The Neon Optimist | Joyful, welcoming, pop/soul |
| Arden Cross | The Roadside Philosopher | Grounded, wandering, blues/folk |
| Nova Six | The Afterhours Futurist | Atmospheric, electronic, observant |

## Integration

The Blazor popup lets visitors select a DJ. The same Ollama endpoint serves all eight profiles but receives a different **system** prompt per DJ, and keeps separate chat histories per selected DJ and per Blazor circuit. Conversations are temporary (lost when the circuit ends).

This is a **private DJ chat prototype**, not a public shared lounge yet.

Before publishing: finish the server-side request limits and abuse protection, verify independent circuits never mix, and run integration tests with actual Ollama responses.

## Installing the portrait assets

The JPG portraits are delivered separately as `aneed_eight_dj_portraits.zip`, because the GitHub text connector cannot upload the generated binary files. Extract the archive at the repository root so you get:

```
AlexiTheMVCBot/wwwroot/img/djs/kathleen.jpg
AlexiTheMVCBot/wwwroot/img/djs/lexi.jpg
AlexiTheMVCBot/wwwroot/img/djs/vesper.jpg
AlexiTheMVCBot/wwwroot/img/djs/roxy.jpg
AlexiTheMVCBot/wwwroot/img/djs/mara.jpg
AlexiTheMVCBot/wwwroot/img/djs/sunny.jpg
AlexiTheMVCBot/wwwroot/img/djs/arden.jpg
AlexiTheMVCBot/wwwroot/img/djs/nova.jpg
```

Then commit and push them on `feature/eight-dj-personas`. Until those files are installed, the selector deliberately falls back to the existing Lexi avatar.

The story that these DJs share a fictional genetic template is a creative direction, not an assertion of actual genetic engineering.
