var React = require('react'); // N.B. Still need this because JSX.
import {
  COLUMNSPACER,
  COLOR_DARK,
  COLOR_VERY_DARK,
} from './style';

export const SettingsButton = ({onClick, text}) => {
  const divStyle = {
    textAlign: 'right',
    marginTop: COLUMNSPACER,
  };
  const style = {
    color: 'white',
    backgroundColor: COLOR_DARK,
    padding: 3,
    border: '2px solid ' + COLOR_VERY_DARK,
    cursor: 'pointer',
  };

  return (
    <div style={divStyle}>
      <span style={style} onClick={onClick}>{text}</span>
    </div>
  );
};

// This one is a class-based component because it maintains the internal
// state of the edit box.
export class SettingInputBox extends React.Component {
  constructor(props) {
    super(props);
    this.state = {value: props.value || ''};
    this.setValue = props.setValue;

    this.handleChange = this.handleChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
  }

  handleChange(event) {
    this.setState({value: event.target.value});
  }

  handleSubmit(event) {
    this.setValue(this.state.value);
    event.preventDefault();
  }

  render() {
    const input_style = {
      width: '100%',
    };

    return <div>
      <input style={input_style} type="text" value={this.state.value} onChange={this.handleChange} />
      <SettingsButton onClick={this.handleSubmit} text={this.props.buttonLabel} />
   </div>;
  }
}
