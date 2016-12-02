var React = require('react'); // N.B. Still need this because JSX.
//import moment from 'moment'

const MsPerSecond = 1000;
const SecondsPerMinute = 60;
const MinutesPerHour = 60;
const HoursPerDay = 60;
const DaysPerWeek = 7;

const MsPerMinute = MsPerSecond * SecondsPerMinute;
const MsPerHour = MsPerMinute * MinutesPerHour;
const MsPerDay = MsPerHour * HoursPerDay;
const MsPerWeek = MsPerDay * DaysPerWeek;


const RelTime = ({time}) => {
    const parsed = Date.parse(time);
    const delta = Date.now() - parsed;
    
    var unit, value;
    if (delta > MsPerWeek) {
        value = Math.round(delta / MsPerWeek);
        unit = 'week';
    } else if (delta > MsPerDay) {
        value = Math.round(delta / MsPerDay);
        unit = 'day';        
    } else if (delta > MsPerHour) {
        value = Math.round(delta / MsPerHour);
        unit = 'hour';
    } else if (delta > MsPerMinute) {
        value = Math.round(delta / MsPerMinute);
        unit = 'minute';
    } else if (delta > MsPerSecond) {
        value = Math.round(delta / MsPerSecond);
        unit = 'second';
    } else {
        return "just now";
    }

    if (value > 1) { unit = unit + "s"; }
    const sentence = value + " " + unit + " ago";

    return <span>{sentence}</span>;
}

export default RelTime;