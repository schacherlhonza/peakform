import { IconActivity, IconBarbell, IconBed, IconBike, IconRun, IconSwimming } from '@tabler/icons-react';
import { SportType } from '../api/generated/models';

/** The sport's icon, shared by every activity/workout row. */
export function sportIcon(sport: SportType | undefined, size = 16) {
  switch (sport) {
    case SportType.Running:
      return <IconRun size={size} />;
    case SportType.Cycling:
      return <IconBike size={size} />;
    case SportType.Swimming:
      return <IconSwimming size={size} />;
    case SportType.Strength:
      return <IconBarbell size={size} />;
    case SportType.Rest:
      return <IconBed size={size} />;
    default:
      return <IconActivity size={size} />;
  }
}
