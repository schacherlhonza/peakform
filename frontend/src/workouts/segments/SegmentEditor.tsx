import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Group, NumberInput, Select, Stack, Text, TextInput } from '@mantine/core';
import { IconArrowDown, IconArrowUp, IconCopy, IconPlus, IconRepeat, IconTrash } from '@tabler/icons-react';
import { IntensityTargetType, WorkoutSegmentType, type WorkoutSegmentDto } from '../../api/generated/models';
import { formatClock } from '../../activities/activityFormat';
import { Badge, Button, FormField, IconButton, SegmentedControl } from '../../design-system/components';
import {
  GARMIN_MAX_STEPS,
  endCondition,
  formatSegmentDistance,
  garminStepCount,
  isBlock,
  parseClock,
  segmentTotals,
  type EndCondition,
} from './segmentFormat';
import classes from './Segments.module.css';

const QUICK_ADD: WorkoutSegmentType[] = [WorkoutSegmentType.WarmUp, WorkoutSegmentType.Main, WorkoutSegmentType.Interval, WorkoutSegmentType.CoolDown];
const BLOCK_QUICK_ADD: WorkoutSegmentType[] = [WorkoutSegmentType.Interval, WorkoutSegmentType.Recovery, WorkoutSegmentType.Rest, WorkoutSegmentType.Main];
const ZONE_NUMBERS = [1, 2, 3, 4, 5, 6, 7];

const newStep = (type: WorkoutSegmentType, order: number): WorkoutSegmentDto => ({
  order,
  type,
  intensityTargetType: IntensityTargetType.Free,
  durationSeconds: type === WorkoutSegmentType.Recovery || type === WorkoutSegmentType.Rest ? 120 : 300,
});

const newBlock = (order: number): WorkoutSegmentDto => ({
  order,
  type: WorkoutSegmentType.Repeat,
  repeatCount: 4,
  intensityTargetType: IntensityTargetType.Free,
  steps: [newStep(WorkoutSegmentType.Interval, 1), newStep(WorkoutSegmentType.Recovery, 2)],
});

/** Time typed as "5" (minutes), "5:30" or "1:05:00"; committed on blur/Enter so half-typed values aren't reformatted. */
function ClockInput({ value, onChange, placeholder, w }: { value: number | null | undefined; onChange: (seconds: number | null) => void; placeholder?: string; w?: number }) {
  // Text being typed; null when not editing, so the field always shows the committed value otherwise.
  const [draft, setDraft] = useState<string | null>(null);
  const [invalid, setInvalid] = useState(false);
  const formatted = value ? formatClock(value) : '';
  const commit = () => {
    if (draft === null) return;
    const seconds = parseClock(draft);
    if (draft.trim() && seconds === null) {
      setInvalid(true); // keep the invalid text visible and flagged
      return;
    }
    onChange(seconds);
    setDraft(null);
  };
  return (
    <TextInput
      w={w}
      value={draft ?? formatted}
      placeholder={placeholder}
      error={invalid}
      onChange={(e) => {
        setDraft(e.currentTarget.value);
        setInvalid(false);
      }}
      onBlur={commit}
      onKeyDown={(e) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          commit();
        }
      }}
    />
  );
}

const numberOrNull = (v: string | number) => (v === '' ? null : Number(v));

/** Generic list operations shared by the top level and the steps of a block. */
function listOps(value: WorkoutSegmentDto[], onChange: (segments: WorkoutSegmentDto[]) => void) {
  return {
    update: (index: number, next: WorkoutSegmentDto) => onChange(value.map((s, i) => (i === index ? next : s))),
    move: (index: number, delta: number) => {
      const next = [...value];
      [next[index], next[index + delta]] = [next[index + delta], next[index]];
      onChange(next);
    },
    duplicate: (index: number) => {
      const copy = { ...value[index], id: null, steps: value[index].steps?.map((s) => ({ ...s, id: null })) };
      onChange([...value.slice(0, index + 1), copy, ...value.slice(index + 1)]);
    },
    remove: (index: number) => onChange(value.filter((_, i) => i !== index)),
  };
}

function RowActions({ index, count, ops }: { index: number; count: number; ops: ReturnType<typeof listOps> }) {
  const { t } = useTranslation();
  return (
    <Group gap={2} wrap="nowrap">
      <IconButton icon={<IconArrowUp size={15} />} label={t('workout.moveUp')} disabled={index === 0} onClick={() => ops.move(index, -1)} />
      <IconButton icon={<IconArrowDown size={15} />} label={t('workout.moveDown')} disabled={index === count - 1} onClick={() => ops.move(index, 1)} />
      <IconButton icon={<IconCopy size={15} />} label={t('workout.duplicateSegment')} onClick={() => ops.duplicate(index)} />
      <IconButton icon={<IconTrash size={15} />} label={t('common.delete')} color="red" onClick={() => ops.remove(index)} />
    </Group>
  );
}

function SegmentCard({ title, actions, children, block = false }: { title: ReactNode; actions: ReactNode; children: ReactNode; block?: boolean }) {
  return (
    <div className={block ? `${classes.editorRow} ${classes.editorBlock}` : classes.editorRow}>
      <Group justify="space-between" wrap="nowrap" gap="xs">
        {title}
        {actions}
      </Group>
      {children}
    </div>
  );
}

/** Fields of one executable step — type, how it ends, intensity target and a note. */
function StepFields({ step, onChange, inBlock }: { step: WorkoutSegmentDto; onChange: (step: WorkoutSegmentDto) => void; inBlock: boolean }) {
  const { t } = useTranslation();
  const typeOptions = Object.values(WorkoutSegmentType)
    .filter((v) => v !== WorkoutSegmentType.Repeat)
    .map((v) => ({ value: v, label: t(`segmentType.${v}`) }));
  const intensityOptions = Object.values(IntensityTargetType).map((v) => ({ value: v, label: t(`intensityTarget.${v}`) }));
  const zoneOptions = ZONE_NUMBERS.map((n) => ({ value: String(n), label: `Z${n}` }));
  const update = (patch: Partial<WorkoutSegmentDto>) => onChange({ ...step, ...patch });
  const ends = endCondition(step);
  const setEnds = (next: EndCondition) =>
    update({
      durationSeconds: next === 'time' ? (step.durationSeconds ?? 300) : null,
      distanceMeters: next === 'distance' ? (step.distanceMeters ?? 1000) : null,
    });

  return (
    <>
      <div className={classes.editorFields}>
        <FormField label={t('workout.segmentType')}>
          <Select w={170} data={typeOptions} value={step.type ?? null} allowDeselect={false} onChange={(v) => v && update({ type: v as WorkoutSegmentType })} />
        </FormField>
        {!inBlock && (
          <FormField label={t('workout.repeat')} unit="×">
            <NumberInput w={80} min={1} max={100} placeholder="1" value={step.repeatCount ?? ''} onChange={(v) => update({ repeatCount: numberOrNull(v) })} />
          </FormField>
        )}
        <FormField label={t('workout.endsBy')}>
          <SegmentedControl
            value={ends}
            onChange={(v) => setEnds(v as EndCondition)}
            data={[
              { value: 'time', label: t('workout.endsByTime') },
              { value: 'distance', label: t('workout.endsByDistance') },
              { value: 'lap', label: t('workout.endsByLap') },
            ]}
          />
        </FormField>
        {ends === 'time' && (
          <FormField label={t('workout.duration')} unit={t('workout.durationFormat')}>
            <ClockInput w={110} placeholder="5:00" value={step.durationSeconds} onChange={(v) => update({ durationSeconds: v })} />
          </FormField>
        )}
        {ends === 'distance' && (
          <FormField label={t('workout.distance')} unit="m">
            <NumberInput w={110} min={1} step={100} value={step.distanceMeters ?? ''} onChange={(v) => update({ distanceMeters: numberOrNull(v) })} />
          </FormField>
        )}
      </div>
      <div className={classes.editorFields}>
        <FormField label={t('workout.target')}>
          <Select
            w={170}
            data={intensityOptions}
            value={step.intensityTargetType ?? IntensityTargetType.Free}
            allowDeselect={false}
            onChange={(v) => v && update({ intensityTargetType: v as IntensityTargetType })}
          />
        </FormField>
        {step.intensityTargetType === IntensityTargetType.HeartRateZone && (
          <FormField label={t('workout.hrZone')}>
            <Select
              w={100}
              data={zoneOptions}
              value={step.targetHeartRateZoneNumber ? String(step.targetHeartRateZoneNumber) : null}
              onChange={(v) => update({ targetHeartRateZoneNumber: v ? Number(v) : null, targetHeartRateZoneId: null })}
            />
          </FormField>
        )}
        {step.intensityTargetType === IntensityTargetType.Pace && (
          <>
            <FormField label={t('workout.paceFrom')} unit="/km">
              <ClockInput w={100} placeholder="4:30" value={step.targetPaceSecondsPerKmMin} onChange={(v) => update({ targetPaceSecondsPerKmMin: v })} />
            </FormField>
            <FormField label={t('workout.paceTo')} unit="/km">
              <ClockInput w={100} placeholder="4:50" value={step.targetPaceSecondsPerKmMax} onChange={(v) => update({ targetPaceSecondsPerKmMax: v })} />
            </FormField>
          </>
        )}
        {step.intensityTargetType === IntensityTargetType.Rpe && (
          <FormField label="RPE" unit="1–10">
            <NumberInput w={90} min={1} max={10} value={step.targetRpe ?? ''} onChange={(v) => update({ targetRpe: numberOrNull(v) })} />
          </FormField>
        )}
        {step.intensityTargetType === IntensityTargetType.Power && (
          <FormField label={t('workout.power')} unit="W">
            <NumberInput w={100} min={0} value={step.targetPowerWatts ?? ''} onChange={(v) => update({ targetPowerWatts: numberOrNull(v) })} />
          </FormField>
        )}
        <div className={classes.editorNotes}>
          <FormField label={t('common.notes')}>
            <TextInput maxLength={500} value={step.notes ?? ''} onChange={(e) => update({ notes: e.currentTarget.value || null })} />
          </FormField>
        </div>
      </div>
    </>
  );
}

function BlockFields({ block, onChange }: { block: WorkoutSegmentDto; onChange: (block: WorkoutSegmentDto) => void }) {
  const { t } = useTranslation();
  const steps = block.steps ?? [];
  const setSteps = (next: WorkoutSegmentDto[]) => onChange({ ...block, steps: next });
  const ops = listOps(steps, setSteps);
  return (
    <Stack gap="xs">
      <FormField label={t('workout.blockRepeat')} unit="×">
        <NumberInput w={90} min={2} max={100} value={block.repeatCount ?? ''} onChange={(v) => onChange({ ...block, repeatCount: numberOrNull(v) })} />
      </FormField>
      {steps.map((step, index) => (
        // Segments have no stable id until saved; position is the identity while editing.
        <SegmentCard key={index} title={<Text className="ds-eyebrow">{t('workout.blockStep', { n: index + 1 })}</Text>} actions={<RowActions index={index} count={steps.length} ops={ops} />}>
          <StepFields step={step} inBlock onChange={(next) => ops.update(index, next)} />
        </SegmentCard>
      ))}
      {steps.length === 0 && <Text className="ds-metadata">{t('workout.blockEmpty')}</Text>}
      <Group gap="xs">
        {BLOCK_QUICK_ADD.map((type) => (
          <Button key={type} variant="default" size="compact-xs" leftSection={<IconPlus size={12} />} onClick={() => setSteps([...steps, newStep(type, steps.length + 1)])}>
            {t(`segmentType.${type}`)}
          </Button>
        ))}
      </Group>
    </Stack>
  );
}

/**
 * Edits a workout's structure — shared by planned workouts and templates. Top-level items are steps or
 * repeat blocks (one level, as intervals.icu/Garmin take them). Controlled: emits the whole list on every
 * change; the caller runs `normalizeSegments` before saving.
 */
export function SegmentEditor({ value, onChange }: { value: WorkoutSegmentDto[]; onChange: (segments: WorkoutSegmentDto[]) => void }) {
  const { t } = useTranslation();
  const ops = listOps(value, onChange);
  const totals = segmentTotals(value);
  const garminSteps = garminStepCount(value);

  return (
    <Stack gap="xs">
      {value.length === 0 && <Text className="ds-metadata">{t('workout.noSegments')}</Text>}
      {value.map((s, index) =>
        isBlock(s) ? (
          <SegmentCard
            key={index}
            block
            title={
              <Group gap={6}>
                <IconRepeat size={15} aria-hidden />
                <Text className="ds-eyebrow">
                  #{index + 1} · {t('workout.blockTitle')}
                </Text>
              </Group>
            }
            actions={<RowActions index={index} count={value.length} ops={ops} />}
          >
            <BlockFields block={s} onChange={(next) => ops.update(index, next)} />
          </SegmentCard>
        ) : (
          <SegmentCard key={index} title={<Text className="ds-eyebrow">#{index + 1}</Text>} actions={<RowActions index={index} count={value.length} ops={ops} />}>
            <StepFields step={s} inBlock={false} onChange={(next) => ops.update(index, next)} />
          </SegmentCard>
        ),
      )}

      <Group gap="xs" mt={4}>
        {QUICK_ADD.map((type) => (
          <Button key={type} variant="default" size="compact-sm" leftSection={<IconPlus size={14} />} onClick={() => onChange([...value, newStep(type, value.length + 1)])}>
            {t(`segmentType.${type}`)}
          </Button>
        ))}
        <Button variant="default" size="compact-sm" leftSection={<IconRepeat size={14} />} onClick={() => onChange([...value, newBlock(value.length + 1)])}>
          {t('workout.addBlock')}
        </Button>
      </Group>

      <Group gap="sm">
        {(totals.durationSeconds || totals.distanceMeters) && (
          <Text className="ds-metadata">
            {t('workout.structureTotal')}:{' '}
            {[totals.durationSeconds ? formatClock(totals.durationSeconds) : null, totals.distanceMeters ? formatSegmentDistance(totals.distanceMeters) : null]
              .filter(Boolean)
              .join(' · ')}
          </Text>
        )}
        {garminSteps > GARMIN_MAX_STEPS && <Badge tone="warning">{t('workout.tooManySteps', { count: garminSteps, max: GARMIN_MAX_STEPS })}</Badge>}
      </Group>
    </Stack>
  );
}
