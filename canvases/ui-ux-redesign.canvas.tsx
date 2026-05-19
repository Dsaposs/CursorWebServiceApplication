import {
  Button,
  Callout,
  Card,
  CardBody,
  CardHeader,
  CollapsibleSection,
  colorPalette,
  Divider,
  Grid,
  H1,
  H2,
  H3,
  Pill,
  Row,
  Spacer,
  Stack,
  Stat,
  Table,
  Text,
  UsageBar,
  useCanvasState,
  useHostTheme,
} from 'cursor/canvas';

type Screen = 'analysis' | 'login' | 'games' | 'dm' | 'player';

// ─── Shared chrome helpers ────────────────────────────────────────────────────

function ScreenFrame({ children }: { children?: any }) {
  const theme = useHostTheme();
  return (
    <div style={{
      border: `1px solid ${theme.stroke.primary}`,
      borderRadius: 10,
      overflow: 'hidden',
      background: theme.bg.editor,
    }}>
      {children}
    </div>
  );
}

function Topbar({
  left,
  center,
  right,
  combatMode,
}: {
  left?: any;
  center?: any;
  right?: any;
  combatMode?: boolean;
}) {
  const theme = useHostTheme();
  return (
    <div style={{
      display: 'flex',
      alignItems: 'center',
      padding: '0 16px',
      height: 48,
      background: theme.bg.chrome,
      borderBottom: combatMode
        ? `2px solid ${theme.diff.stripRemoved}`
        : `1px solid ${theme.stroke.primary}`,
      gap: 12,
      flexShrink: 0,
    }}>
      {left}
      {center && <div style={{ flex: 1, display: 'flex', justifyContent: 'center' }}>{center}</div>}
      {right && (
        <div style={{ marginLeft: center ? 0 : 'auto', display: 'flex', gap: 8, alignItems: 'center' }}>
          {right}
        </div>
      )}
    </div>
  );
}

function Wordmark() {
  const theme = useHostTheme();
  return (
    <span style={{
      fontSize: 11,
      fontWeight: 800,
      letterSpacing: '0.14em',
      textTransform: 'uppercase',
      color: theme.accent.primary,
      borderBottom: `2px solid ${theme.accent.primary}`,
      paddingBottom: 1,
      userSelect: 'none',
    }}>
      TTRPG TABLE
    </span>
  );
}

function MockSidebar({ children }: { children?: any }) {
  const theme = useHostTheme();
  return (
    <div style={{
      width: 216,
      flexShrink: 0,
      background: theme.bg.chrome,
      borderRight: `1px solid ${theme.stroke.primary}`,
      padding: '12px 8px',
      overflowY: 'auto',
    }}>
      {children}
    </div>
  );
}

function FieldLabel({ children }: { children?: any }) {
  const theme = useHostTheme();
  return (
    <div style={{
      fontSize: 10,
      fontWeight: 700,
      letterSpacing: '0.08em',
      textTransform: 'uppercase',
      color: theme.text.tertiary,
      marginBottom: 5,
    }}>
      {children}
    </div>
  );
}

function MockInput({ placeholder }: { placeholder?: string }) {
  const theme = useHostTheme();
  return (
    <div style={{
      height: 34,
      borderRadius: 7,
      border: `1px solid ${theme.stroke.primary}`,
      background: theme.bg.chrome,
      display: 'flex',
      alignItems: 'center',
      paddingLeft: 10,
      fontSize: 13,
      color: theme.text.tertiary,
    }}>
      {placeholder}
    </div>
  );
}

function MockTextarea() {
  const theme = useHostTheme();
  return (
    <div style={{
      height: 60,
      borderRadius: 7,
      border: `1px solid ${theme.stroke.primary}`,
      background: theme.bg.chrome,
    }} />
  );
}

function HpBar({ hp, maxHp }: { hp: number; maxHp: number }) {
  const pct = hp / maxHp;
  const color: 'green' | 'orange' | 'pink' =
    pct >= 0.65 ? 'green' : pct >= 0.35 ? 'orange' : 'pink';
  return (
    <UsageBar
      total={maxHp}
      topLeftLabel={<Text size="small" tone="secondary">{hp} / {maxHp} HP</Text>}
      segments={[{ id: 'hp', value: hp, color }]}
    />
  );
}

// ─── Screen: Analysis ────────────────────────────────────────────────────────

function AnalysisScreen() {
  return (
    <Stack gap={24}>
      <Grid columns={2} gap={16}>
        <Card>
          <CardHeader>Visual Issues</CardHeader>
          <CardBody>
            <Stack gap={10}>
              {[
                ['Typography', 'Heading scale spans only 0.5 rem — h1 (1.5) through h3 (1.0) feel identical at a glance'],
                ['Icons', 'Emoji used as section markers (⚔️ 🎲 🧙) — rendering differs across OS and screen'],
                ['Surfaces', 'All panels share the same --panel background and --border — no elevation hierarchy'],
                ['Accent overuse', 'Gold (#e8a32a) on primary buttons, links, tabs, active states, and badges — loses signal'],
                ['Shadows', 'box-shadow on panels creates floating effect inconsistent with the flat dark theme'],
              ].map(([label, note]) => (
                <Row key={label} gap={8} align="start">
                  <Pill tone="warning" size="sm" style={{ flexShrink: 0 }}>{label}</Pill>
                  <Text size="small" tone="secondary">{note}</Text>
                </Row>
              ))}
            </Stack>
          </CardBody>
        </Card>

        <Card>
          <CardHeader>UX Issues</CardHeader>
          <CardBody>
            <Stack gap={10}>
              {[
                ['High', 'Combat and Exploration modes use identical chrome — session state is invisible in peripheral vision'],
                ['High', 'Pending action queue has no urgency differentiation from the published action log below it'],
                ['High', 'Player screen: the action form sits below the character sheet, requiring scroll to reach it'],
                ['Medium', 'HP bars are styled with the same weight as form labels — critical health is not alarming'],
                ['Medium', 'Primary CTA (Start Session / Resolve) competes visually with 4–5 ghost buttons nearby'],
                ['Low', 'Login page uses an emoji as its brand mark — inconsistent with the serious game content'],
              ].map(([level, note]) => (
                <Row key={note.slice(0, 20)} gap={8} align="start">
                  <Pill
                    tone={level === 'High' ? 'danger' : level === 'Medium' ? 'warning' : 'neutral'}
                    size="sm"
                    active={level === 'High'}
                    style={{ flexShrink: 0 }}
                  >{level}</Pill>
                  <Text size="small" tone="secondary">{note}</Text>
                </Row>
              ))}
            </Stack>
          </CardBody>
        </Card>
      </Grid>

      <Divider />

      <H2>Proposed Design Principles</H2>
      <Grid columns={3} gap={16}>
        {[
          ['Reserve the Gold', 'Accent (#e8a32a) only on the one primary action per context. All secondary actions use ghost style. Gold means "do this now" — nothing else.'],
          ['State-Driven Chrome', 'Combat mode gets a red bottom border on the topbar. Exploration uses neutral chrome. Session state must be visible in peripheral vision without reading text.'],
          ['Breathing Room in Type', 'Increase h1 to 2rem, h2 to 1.4rem. Use the muted-light consistently for body copy; ink-bright only for headings and values. Section labels use uppercase + tracking.'],
          ['Remove Emoji Icons', 'Replace decorative emoji with text-based status pills or geometric indicators. The brand mark on login becomes a typographic wordmark with underline accent.'],
          ['Action State Colors', 'Pending queue: orange-tinted panel. Published (resolved): green-tinted. Rejected: red-tinted. These tokens already exist in the CSS — apply them consistently.'],
          ['Flat Elevation Model', 'Remove box-shadow from all panels. Use background tint steps (--bg → --panel → --panel-alt) for elevation. Borders are structural and use one weight.'],
        ].map(([title, body]) => (
          <Stack key={title} gap={6}>
            <Text weight="semibold">{title}</Text>
            <Text size="small" tone="secondary">{body}</Text>
          </Stack>
        ))}
      </Grid>

      <Divider />

      <H2>Prioritized Changes</H2>
      <Table
        headers={['Screen', 'Change', 'Effort', 'Impact']}
        rows={[
          ['DM Session', 'Combat/Exploration topbar mode indicator — 2px colored border + pill badge', 'XS', 'High'],
          ['DM Session', 'Pending action queue: warm orange tint on the panel background', 'XS', 'High'],
          ['Player Screen', 'Move action form above character sheet; collapse sheet by default during session', 'S', 'High'],
          ['All screens', 'Remove box-shadow from .panel; update typography scale (h1 2rem, h2 1.4rem)', 'S', 'Medium'],
          ['All screens', 'Reserve .btn (gold) for one primary CTA per screen; demote others to .btn.ghost', 'S', 'Medium'],
          ['Games Hub', 'Add ruleset pill badge to each game list item in the sidebar', 'XS', 'Medium'],
          ['Games Hub', 'Replace ghost-button tab bar with pill-style segmented tabs', 'XS', 'Medium'],
          ['Login', 'Typographic wordmark + segmented tab control instead of emoji + ghost tabs', 'XS', 'Low'],
        ]}
        rowTone={['warning', 'warning', 'warning', undefined, undefined, undefined, undefined, undefined]}
      />
    </Stack>
  );
}

// ─── Screen: Login ────────────────────────────────────────────────────────────

function LoginScreen() {
  const theme = useHostTheme();

  return (
    <Stack gap={24}>
      <Grid columns={2} gap={24}>
        {/* Before */}
        <Stack gap={10}>
          <Row gap={8} align="center">
            <Pill tone="warning" active size="sm">Current</Pill>
          </Row>
          <ScreenFrame>
            <div style={{
              minHeight: 400,
              display: 'grid',
              placeItems: 'center',
              padding: 32,
              background: theme.bg.editor,
            }}>
              <div style={{
                width: 300,
                padding: 28,
                border: `1px solid ${theme.stroke.primary}`,
                borderRadius: 18,
                background: theme.bg.elevated,
              }}>
                <div style={{ textAlign: 'center', marginBottom: 18 }}>
                  <div style={{ fontSize: 30, marginBottom: 6 }}>⚔️</div>
                  <div style={{ fontWeight: 700, fontSize: 17, color: theme.text.primary, marginBottom: 3 }}>TTRPG Table</div>
                  <div style={{ fontSize: 12, color: theme.text.tertiary }}>Sign in to host your games.</div>
                </div>
                <div style={{ display: 'flex', gap: 6, marginBottom: 14, padding: 4, border: `1px solid ${theme.stroke.primary}`, borderRadius: 8 }}>
                  <div style={{ flex: 1, textAlign: 'center', padding: '6px 0', borderRadius: 6, background: theme.fill.secondary, fontSize: 12, fontWeight: 600, color: theme.text.primary }}>Sign In</div>
                  <div style={{ flex: 1, textAlign: 'center', padding: '6px 0', fontSize: 12, color: theme.text.tertiary }}>Register</div>
                </div>
                <Stack gap={10}>
                  <div><div style={{ fontSize: 12, color: theme.text.secondary, marginBottom: 4 }}>Email</div><MockInput /></div>
                  <div><div style={{ fontSize: 12, color: theme.text.secondary, marginBottom: 4 }}>Password</div><MockInput /></div>
                  <div style={{ height: 36, borderRadius: 8, background: theme.accent.control, marginTop: 4 }} />
                </Stack>
                <div style={{ textAlign: 'center', marginTop: 12, fontSize: 11, color: theme.text.tertiary }}>
                  Players don't need an account — join via invite link.
                </div>
              </div>
            </div>
          </ScreenFrame>
          <Stack gap={6}>
            <Row gap={6} align="start"><Pill tone="warning" size="sm" style={{ flexShrink: 0 }}>Icon</Pill><Text size="small" tone="secondary">Emoji renders differently on Windows, macOS, Android</Text></Row>
            <Row gap={6} align="start"><Pill tone="warning" size="sm" style={{ flexShrink: 0 }}>Tabs</Pill><Text size="small" tone="secondary">Ghost-button pair looks like two independent actions, not a selector</Text></Row>
            <Row gap={6} align="start"><Pill tone="warning" size="sm" style={{ flexShrink: 0 }}>Hierarchy</Pill><Text size="small" tone="secondary">App name and "Sign in to host" have almost identical visual weight</Text></Row>
          </Stack>
        </Stack>

        {/* After */}
        <Stack gap={10}>
          <Row gap={8} align="center">
            <Pill tone="success" active size="sm">Proposed</Pill>
          </Row>
          <ScreenFrame>
            <div style={{
              minHeight: 400,
              display: 'grid',
              placeItems: 'center',
              padding: 32,
              background: theme.bg.editor,
            }}>
              <div style={{
                width: 300,
                padding: 28,
                border: `1px solid ${theme.stroke.primary}`,
                borderRadius: 18,
                background: theme.bg.elevated,
              }}>
                <div style={{ textAlign: 'center', marginBottom: 22 }}>
                  <div style={{ marginBottom: 10 }}><Wordmark /></div>
                  <div style={{ fontWeight: 700, fontSize: 19, color: theme.text.primary, marginBottom: 5 }}>Welcome back</div>
                  <div style={{ fontSize: 13, color: theme.text.secondary }}>Sign in to manage your campaigns.</div>
                </div>
                {/* Segmented tab */}
                <div style={{
                  display: 'flex',
                  gap: 3,
                  marginBottom: 18,
                  padding: 3,
                  background: theme.bg.chrome,
                  borderRadius: 9,
                  border: `1px solid ${theme.stroke.secondary}`,
                }}>
                  <div style={{ flex: 1, textAlign: 'center', padding: '7px 0', borderRadius: 7, background: theme.bg.elevated, fontSize: 12, fontWeight: 600, color: theme.text.primary }}>Sign In</div>
                  <div style={{ flex: 1, textAlign: 'center', padding: '7px 0', fontSize: 12, color: theme.text.tertiary }}>Register</div>
                </div>
                <Stack gap={12}>
                  <div><FieldLabel>Email</FieldLabel><MockInput placeholder="dm@example.com" /></div>
                  <div><FieldLabel>Password</FieldLabel><MockInput /></div>
                  <div style={{ height: 38, borderRadius: 8, background: theme.accent.control, marginTop: 4 }} />
                </Stack>
                <div style={{
                  marginTop: 14,
                  padding: '9px 12px',
                  borderRadius: 8,
                  background: theme.bg.chrome,
                  border: `1px solid ${theme.stroke.secondary}`,
                  fontSize: 12,
                  color: theme.text.tertiary,
                  textAlign: 'center',
                }}>
                  Players join via invite — no account needed.
                </div>
              </div>
            </div>
          </ScreenFrame>
          <Stack gap={6}>
            <Row gap={6} align="start"><Pill tone="success" size="sm" style={{ flexShrink: 0 }}>Fix</Pill><Text size="small" tone="secondary">Text wordmark with accent underline — consistent, professional</Text></Row>
            <Row gap={6} align="start"><Pill tone="success" size="sm" style={{ flexShrink: 0 }}>Fix</Pill><Text size="small" tone="secondary">Segmented control clearly shows selection state; feels native</Text></Row>
            <Row gap={6} align="start"><Pill tone="success" size="sm" style={{ flexShrink: 0 }}>Fix</Pill><Text size="small" tone="secondary">Uppercase micro-labels for fields; "Welcome back" as the page heading</Text></Row>
          </Stack>
        </Stack>
      </Grid>
    </Stack>
  );
}

// ─── Screen: Games Hub ────────────────────────────────────────────────────────

function GamesScreen() {
  const theme = useHostTheme();

  const games = [
    { name: 'The Lost Sanctum', ruleset: 'D&D 5e', rulesetTone: 'info' as const, live: true },
    { name: 'Nostromo Incident', ruleset: 'Alien RPG', rulesetTone: 'danger' as const, live: false },
    { name: 'Shattered Realms', ruleset: 'D&D 5e', rulesetTone: 'info' as const, live: false },
  ];

  return (
    <Stack gap={16}>
      <Text tone="secondary" size="small">Games Hub — Proposed Layout</Text>
      <ScreenFrame>
        <Topbar
          left={<Wordmark />}
          right={
            <Row gap={6} align="center">
              <Text size="small" tone="tertiary">dm@example.com</Text>
              <Button variant="ghost">Rulesets</Button>
              <Button variant="ghost">Sign out</Button>
            </Row>
          }
        />
        <div style={{ display: 'flex', minHeight: 540 }}>
          <MockSidebar>
            <Stack gap={12}>
              <Row align="center" justify="space-between" style={{ padding: '0 4px' }}>
                <Text size="small" weight="semibold" style={{ textTransform: 'uppercase', letterSpacing: '0.08em', color: theme.text.tertiary }}>
                  MY GAMES
                </Text>
                <Button variant="primary">+ New</Button>
              </Row>
              <Stack gap={4}>
                {games.map((g, i) => (
                  <div key={g.name} style={{
                    padding: '9px 10px',
                    borderRadius: 8,
                    border: `1px solid ${i === 0 ? theme.accent.primary : 'transparent'}`,
                    background: i === 0 ? theme.fill.secondary : 'transparent',
                    cursor: 'pointer',
                  }}>
                    <div style={{ fontWeight: 600, fontSize: 13, color: theme.text.primary, marginBottom: 4 }}>{g.name}</div>
                    <Row gap={5} align="center">
                      <Pill size="sm" tone={g.rulesetTone}>{g.ruleset}</Pill>
                      {g.live && <Pill size="sm" tone="success" active>Live</Pill>}
                    </Row>
                  </div>
                ))}
              </Stack>
            </Stack>
          </MockSidebar>

          <div style={{ flex: 1, padding: '16px 20px', background: theme.bg.editor, overflowY: 'auto' }}>
            <Stack gap={18}>
              {/* Game header */}
              <Row align="center" gap={12}>
                <Stack gap={5}>
                  <H2>The Lost Sanctum</H2>
                  <Row gap={6} align="center">
                    <Pill size="sm" tone="info">D&D 5e</Pill>
                    <Pill size="sm" tone="success" active>Session Active</Pill>
                  </Row>
                </Stack>
                <Spacer />
                <Button variant="primary">Continue Session</Button>
                <Button variant="ghost">Delete</Button>
              </Row>

              {/* Tab bar — pill style */}
              <Row gap={5}>
                {['Overview', 'Players', 'NPCs', 'Notes'].map((tab, i) => (
                  <Pill key={tab} active={i === 0}>{tab}</Pill>
                ))}
              </Row>

              <Divider />

              <Grid columns={4} gap={12}>
                <Stat value="4" label="Players" />
                <Stat value="7" label="Sessions" />
                <Stat value="3" label="NPCs" />
                <Stat value="1h 42m" label="Avg Duration" />
              </Grid>

              <Stack gap={8}>
                <H3>Invite Players</H3>
                <div style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 10,
                  padding: '9px 12px',
                  borderRadius: 8,
                  border: `1px solid ${theme.stroke.primary}`,
                  background: theme.bg.chrome,
                }}>
                  <Text size="small" tone="tertiary" style={{ flex: 1, fontFamily: 'monospace' }}>
                    https://ttrpg.app/join/game/XK2A9B
                  </Text>
                  <Button variant="secondary">Copy</Button>
                </div>
              </Stack>

              <Stack gap={8}>
                <H3>Session History</H3>
                <Table
                  headers={['Session', 'Date', 'Duration', 'Players']}
                  rows={[
                    ['Session 7 — current', 'May 19, 2026', 'In progress', '4'],
                    ['Session 6', 'May 16, 2026', '2h 14m', '4'],
                    ['Session 5', 'May 9, 2026', '1h 51m', '3'],
                  ]}
                  rowTone={['info', undefined, undefined]}
                  striped
                />
              </Stack>
            </Stack>
          </div>
        </div>
      </ScreenFrame>

      <Grid columns={2} gap={16}>
        <Callout tone="success" title="Sidebar improvements">
          Each game shows a ruleset badge (D&D 5e, Alien RPG) and a live indicator pill. Sidebar items have a clear accent border on the selected item. The "+ New" button is a primary button, not ghost.
        </Callout>
        <Callout tone="info" title="Dashboard improvements">
          Pill-style tabs replace ghost buttons. Stats strip gives campaign overview at a glance. The active session row in the history table is highlighted (info tone) with "In progress" duration.
        </Callout>
      </Grid>
    </Stack>
  );
}

// ─── Screen: DM Live Session ──────────────────────────────────────────────────

function DMScreen() {
  const theme = useHostTheme();

  return (
    <Stack gap={16}>
      <Row gap={8} align="center">
        <Text tone="secondary" size="small">DM Live Session — Combat Mode</Text>
        <Spacer />
        <Pill tone="danger" active>COMBAT</Pill>
        <Text size="small" tone="tertiary">Red topbar border signals mode in peripheral vision</Text>
      </Row>

      <ScreenFrame>
        {/* Combat-mode topbar — 2px danger border */}
        <Topbar
          combatMode
          left={<Row gap={10} align="center"><Wordmark /><Pill tone="danger" active>COMBAT</Pill></Row>}
          center={<Text size="small" weight="semibold" tone="secondary">The Lost Sanctum — Session 8</Text>}
          right={
            <Row gap={6} align="center">
              <Pill size="sm" tone="success">Connected · 4 players</Pill>
              <Text size="small" tone="tertiary">1h 23m</Text>
              <Button variant="ghost">Stop Session</Button>
            </Row>
          }
        />

        <div style={{ display: 'flex', minHeight: 580 }}>
          {/* Primary column */}
          <div style={{ flex: 1, padding: 16, overflowY: 'auto' }}>
            <Stack gap={16}>

              {/* Initiative */}
              <Card>
                <CardHeader trailing={
                  <Row gap={6}>
                    <Pill size="sm" tone="danger" active>Round 3</Pill>
                    <Button variant="secondary">Next Turn</Button>
                    <Button variant="ghost">End Combat</Button>
                  </Row>
                }>Combat Initiative</CardHeader>
                <CardBody style={{ padding: 0 }}>
                  <Table
                    headers={['#', 'Combatant', 'Type', 'HP', 'Turn']}
                    rows={[
                      ['1', 'Aldric (Fighter)', 'PC', '28/45', 'ACTING'],
                      ['2', 'Shadowstalker', 'NPC', '18/30', ''],
                      ['3', 'Lyra (Rogue)', 'PC', '35/40', ''],
                      ['4', 'Guard Captain', 'NPC', '12/50', ''],
                    ]}
                    rowTone={['warning', undefined, undefined, undefined]}
                  />
                </CardBody>
              </Card>

              {/* Player participant cards */}
              <H3>Participants</H3>
              <Grid columns={2} gap={12}>
                {[
                  { name: 'Aldric', cls: 'Fighter Lv.4', hp: 28, maxHp: 45, armor: 3, stress: 0 },
                  { name: 'Lyra', cls: 'Rogue Lv.4', hp: 35, maxHp: 40, armor: 1, stress: 2 },
                ].map(p => (
                  <Card key={p.name}>
                    <CardHeader trailing={<Pill size="sm" tone="info">PC</Pill>}>{p.name} — {p.cls}</CardHeader>
                    <CardBody>
                      <Stack gap={10}>
                        <Row gap={16} align="center">
                          <Stat
                            value={`${p.hp}/${p.maxHp}`}
                            label="HP"
                            tone={p.hp / p.maxHp < 0.4 ? 'danger' : p.hp / p.maxHp < 0.65 ? 'warning' : 'success'}
                          />
                          <Stat value={String(p.armor)} label="Armor" />
                          {p.stress > 0 && <Stat value={String(p.stress)} label="Stress" tone="warning" />}
                        </Row>
                        <HpBar hp={p.hp} maxHp={p.maxHp} />
                        <Row gap={6}>
                          <Button variant="ghost">Adjust Stats</Button>
                          <Button variant="ghost">Inventory</Button>
                        </Row>
                      </Stack>
                    </CardBody>
                  </Card>
                ))}
              </Grid>

              {/* NPC action entry */}
              <Card collapsible defaultOpen={false}>
                <CardHeader>Log NPC Action</CardHeader>
                <CardBody>
                  <Text tone="secondary" size="small">Select an NPC, choose its action, and set a target to publish to the log.</Text>
                </CardBody>
              </Card>
            </Stack>
          </div>

          {/* Feed column */}
          <div style={{
            width: 340,
            flexShrink: 0,
            borderLeft: `1px solid ${theme.stroke.primary}`,
            background: theme.bg.chrome,
            display: 'flex',
            flexDirection: 'column',
            overflowY: 'auto',
          }}>
            {/* Pending queue — orange tint */}
            <div style={{
              borderBottom: `1px solid ${theme.stroke.primary}`,
              padding: 12,
              background: `${colorPalette.orange}12`,
            }}>
              <Stack gap={10}>
                <Row gap={8} align="center">
                  <Text size="small" weight="semibold" style={{ textTransform: 'uppercase', letterSpacing: '0.07em' }}>
                    Pending
                  </Text>
                  <Pill tone="warning" active size="sm">2</Pill>
                </Row>

                {[
                  { player: 'Aldric', action: 'Melee Attack', target: 'Shadowstalker', roll: 14 },
                  { player: 'Lyra', action: 'Sneak Attack', target: 'Guard Captain', roll: 18 },
                ].map((a, i) => (
                  <Card key={i}>
                    <CardHeader trailing={<Pill size="sm" tone="warning">Pending</Pill>}>{a.player}</CardHeader>
                    <CardBody>
                      <Stack gap={8}>
                        <Row gap={6} wrap align="center">
                          <Text size="small">{a.action}</Text>
                          <Text size="small" tone="tertiary">on</Text>
                          <Text size="small">{a.target}</Text>
                          <Pill size="sm" tone="info">Roll: {a.roll}</Pill>
                        </Row>
                        <Row gap={6}>
                          <Button variant="primary">Resolve</Button>
                          <Button variant="ghost">Reject</Button>
                        </Row>
                      </Stack>
                    </CardBody>
                  </Card>
                ))}
              </Stack>
            </div>

            {/* Action log */}
            <div style={{ flex: 1, padding: 12 }}>
              <Stack gap={10}>
                <Row gap={8} align="center">
                  <Text size="small" weight="semibold" style={{ textTransform: 'uppercase', letterSpacing: '0.07em' }}>
                    Log
                  </Text>
                  <Pill size="sm">8 actions</Pill>
                </Row>
                {[
                  { player: 'Aldric', summary: 'Melee Attack on Guard Captain', outcome: 'Hit — 12 damage', success: true },
                  { player: 'Shadowstalker', summary: 'NPC: Bite on Lyra', outcome: 'Miss', success: false },
                  { player: 'Lyra', summary: 'Backstab on Shadowstalker', outcome: 'Hit — 18 damage', success: true },
                ].map((a, i) => (
                  <Card key={i} collapsible defaultOpen={i === 0}>
                    <CardHeader trailing={
                      <Pill size="sm" tone={a.success ? 'success' : 'neutral'} active={a.success}>
                        {a.success ? 'Hit' : 'Miss'}
                      </Pill>
                    }>{a.player}</CardHeader>
                    <CardBody>
                      <Stack gap={4}>
                        <Text size="small">{a.summary}</Text>
                        <Text size="small" tone="secondary">{a.outcome}</Text>
                      </Stack>
                    </CardBody>
                  </Card>
                ))}
              </Stack>
            </div>
          </div>
        </div>
      </ScreenFrame>

      <Grid columns={3} gap={16}>
        <Callout tone="danger" title="Combat topbar">
          The 2px red border replaces the normal 1px neutral border in combat mode — a peripheral-vision cue that costs zero screen space.
        </Callout>
        <Callout tone="warning" title="Pending queue">
          Pending actions live in an orange-tinted region at the top of the feed column — higher visual urgency than the log below it. Count badge stays visible at a glance.
        </Callout>
        <Callout tone="success" title="HP display">
          Stat component shows value with semantic tone (green/orange/red). UsageBar below gives proportional context. Together they communicate urgency without needing a label.
        </Callout>
      </Grid>
    </Stack>
  );
}

// ─── Screen: Player Session ───────────────────────────────────────────────────

function PlayerScreen() {
  const theme = useHostTheme();

  return (
    <Stack gap={16}>
      <Text tone="secondary" size="small">Player Screen — Aldric's Turn in Combat</Text>

      <ScreenFrame>
        <Topbar
          combatMode
          left={<Row gap={10} align="center"><Wordmark /><Pill tone="danger" active>COMBAT</Pill></Row>}
          right={
            <Row gap={6} align="center">
              <Pill size="sm" tone="success">Connected</Pill>
              <Text size="small" tone="tertiary">Session 8 · Round 3</Text>
            </Row>
          }
        />

        <div style={{ padding: 16, background: theme.bg.editor }}>
          <Stack gap={16}>

            {/* YOUR TURN banner — only visible when it's the player's turn */}
            <Callout tone="warning" title="Your Turn">
              Aldric — you are next in the initiative order. Submit your action before the DM advances the round.
            </Callout>

            {/* Character summary */}
            <Card>
              <CardHeader trailing={<Pill size="sm" tone="info">Fighter Lv.4</Pill>}>Aldric</CardHeader>
              <CardBody>
                <Grid columns={3} gap={16}>
                  <Stack gap={8}>
                    <Stat value="28 / 45" label="HP" tone="warning" />
                    <HpBar hp={28} maxHp={45} />
                  </Stack>
                  <Stat value="3" label="Armor" />
                  <Stat value="1" label="Actions Remaining" />
                </Grid>
              </CardBody>
            </Card>

            {/* Action form — at the top, not scrolled off */}
            <H3>Submit Action</H3>
            <Grid columns="1fr 1fr" gap={16}>
              <Card>
                <CardHeader>Action Details</CardHeader>
                <CardBody>
                  <Stack gap={12}>
                    <div>
                      <FieldLabel>Action Type</FieldLabel>
                      <div style={{ height: 34, borderRadius: 7, border: `1px solid ${theme.stroke.primary}`, background: theme.bg.chrome, display: 'flex', alignItems: 'center', paddingLeft: 10 }}>
                        <Text size="small">Melee Attack</Text>
                      </div>
                    </div>
                    <div>
                      <FieldLabel>Target</FieldLabel>
                      <div style={{ height: 34, borderRadius: 7, border: `1px solid ${theme.stroke.primary}`, background: theme.bg.chrome, display: 'flex', alignItems: 'center', paddingLeft: 10 }}>
                        <Text size="small">Shadowstalker</Text>
                      </div>
                    </div>
                    <div>
                      <FieldLabel>Description (optional)</FieldLabel>
                      <MockTextarea />
                    </div>
                  </Stack>
                </CardBody>
              </Card>

              <Card>
                <CardHeader>Dice Roll</CardHeader>
                <CardBody>
                  <Stack gap={12}>
                    <Text size="small" tone="secondary">D20 + Strength modifier (+3)</Text>
                    <div style={{
                      padding: '16px 0',
                      textAlign: 'center',
                      border: `1px solid ${theme.stroke.primary}`,
                      borderRadius: 9,
                      background: theme.bg.chrome,
                    }}>
                      <div style={{ fontSize: 32, fontWeight: 800, color: theme.text.primary, lineHeight: 1 }}>—</div>
                      <div style={{ fontSize: 11, color: theme.text.tertiary, marginTop: 5 }}>Roll to set your result</div>
                    </div>
                    <Button variant="secondary">Roll d20</Button>
                    <Divider />
                    <Button variant="primary">Submit Action</Button>
                  </Stack>
                </CardBody>
              </Card>
            </Grid>

            {/* My pending actions */}
            <H3>My Pending Actions</H3>
            <Text size="small" tone="tertiary">No actions waiting for DM resolution.</Text>

            {/* Character sheet — collapsed by default during session */}
            <Card collapsible defaultOpen={false}>
              <CardHeader trailing={<Text size="small" tone="tertiary">Fighter Lv.4</Text>}>Character Sheet</CardHeader>
              <CardBody>
                <Text tone="secondary" size="small">Stats, skills, abilities, and inventory — expand to view during session.</Text>
              </CardBody>
            </Card>

            {/* Action feed */}
            <Card collapsible defaultOpen={false}>
              <CardHeader trailing={<Pill size="sm">8 actions</Pill>}>Session Log</CardHeader>
              <CardBody>
                <Text size="small" tone="secondary">All resolved actions from this session.</Text>
              </CardBody>
            </Card>

          </Stack>
        </div>
      </ScreenFrame>

      <Grid columns={2} gap={16}>
        <Callout tone="warning" title="Action form at the top">
          The action form appears above the character sheet during live sessions. The sheet defaults to collapsed — players don't need it during combat and can expand it on demand without scrolling.
        </Callout>
        <Callout tone="info" title="Your Turn banner">
          The full-width warning callout only appears when it is this player's initiative turn. In exploration mode the form is simply always accessible with no banner — no urgency without cause.
        </Callout>
      </Grid>
    </Stack>
  );
}

// ─── Root ─────────────────────────────────────────────────────────────────────

export default function UIUXRedesign() {
  const [screen, setScreen] = useCanvasState<Screen>('screen', 'analysis');

  const tabs: { id: Screen; label: string }[] = [
    { id: 'analysis', label: 'Analysis' },
    { id: 'login', label: 'Login' },
    { id: 'games', label: 'Games Hub' },
    { id: 'dm', label: 'DM Screen' },
    { id: 'player', label: 'Player Screen' },
  ];

  return (
    <Stack gap={24} style={{ padding: 24 }}>
      <Row align="center" gap={12} wrap>
        <Stack gap={2}>
          <H1>TTRPG Table — UI/UX Redesign</H1>
          <Text tone="tertiary" size="small">Mockup · May 2026 · No code changes required to evaluate</Text>
        </Stack>
        <Spacer />
        <Row gap={5} wrap>
          {tabs.map(t => (
            <Pill key={t.id} active={screen === t.id} onClick={() => setScreen(t.id)}>
              {t.label}
            </Pill>
          ))}
        </Row>
      </Row>

      <Divider />

      {screen === 'analysis' && <AnalysisScreen />}
      {screen === 'login' && <LoginScreen />}
      {screen === 'games' && <GamesScreen />}
      {screen === 'dm' && <DMScreen />}
      {screen === 'player' && <PlayerScreen />}
    </Stack>
  );
}
