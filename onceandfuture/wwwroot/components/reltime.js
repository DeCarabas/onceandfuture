// @format
// @flow
import React from "react";

const MsPerSecond = 1000;
const SecondsPerMinute = 60;
const MinutesPerHour = 60;
const HoursPerDay = 24;
const DaysPerWeek = 7;

const MsPerMinute = MsPerSecond * SecondsPerMinute;
const MsPerHour = MsPerMinute * MinutesPerHour;
const MsPerDay = MsPerHour * HoursPerDay;
const MsPerWeek = MsPerDay * DaysPerWeek;

// We use 400 years so that we can hit every single leap year rule, and then:
// 400 years have 146097 days
// 400 years have 4800 months
function daysToMonths(days) {
  return days * 4800 / 146097;
}

/*::
type Props = {
  time: string
};

type State = {
  now: number
};
*/

const round = Math.round;

class RelTime extends React.Component /*::<Props, State>*/ {
  /*::
  timerID: TimeoutID;
  */

  constructor(props /*: Props*/) {
    super(props);
    this.state = { now: Date.now() };
  }

  componentDidMount() {
    const interval = this.nextIntervalMs(Date.now());
    this.timerID = setTimeout(() => this.tick(), interval);
  }

  componentWillUnmount() {
    clearTimeout(this.timerID);
  }

  tick() {
    const now = Date.now();
    const interval = this.nextIntervalMs(now);

    this.setState({ now });
    this.timerID = setTimeout(() => this.tick(), interval);
  }

  nextIntervalMs(now /*: number*/) {
    const parsed = Date.parse(this.props.time);
    const delta = now - parsed;
    if (delta > MsPerWeek) {
      return MsPerWeek;
    }
    if (delta > MsPerDay) {
      return MsPerDay;
    }
    if (delta > MsPerHour) {
      return MsPerHour;
    }

    // Watching the seconds tick by is fun and all, but let's not go
    // crazy. Minute-by-minute updates are fine, and don't ruin anything
    // really.
    return MsPerMinute;
  }

  render() {
    const time = this.props.time;
    const parsed = Date.parse(time);
    const delta = this.state.now - parsed;

    // + " (" + this.props.time + ")";
    const sentence = this.sentenceForDelta(delta);

    return <span>{sentence}</span>;
  }

  sentenceForDelta(delta /*: number*/) {
    const seconds = round(delta / MsPerSecond);
    const minutes = round(delta / MsPerMinute);
    const hours = round(delta / MsPerHour);
    const days = round(delta / MsPerDay);
    const months = round(daysToMonths(delta / MsPerDay));
    const years = round(daysToMonths(delta / MsPerDay) / 12);

    if (seconds < 44) {
      return "a few seconds ago";
    }
    if (seconds < 45) {
      return seconds + " seconds ago";
    }
    if (minutes <= 1) {
      return "one minute ago";
    }
    if (minutes < 45) {
      return minutes + " minutes ago";
    }
    if (hours <= 1) {
      return "one hour ago";
    }
    if (hours < 22) {
      return hours + " hours ago";
    }
    if (days <= 1) {
      return "yesterday";
    }
    if (days < 26) {
      return days + " days ago";
    }
    if (months <= 1) {
      return "last month";
    }
    if (months < 11) {
      return months + " months ago";
    }
    if (years <= 1) {
      return "last year";
    }
    return years + " years ago";
  }
}

export default RelTime;
