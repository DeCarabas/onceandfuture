import React from 'react';
import {
  COLOR_DARK,
  COLOR_VERY_DARK,

  SIZE_SPACER_HEIGHT,
} from './style';


export const SettingsSectionTitle = ({text}) => {
  const style = {
    fontWeight: 'bold',
  };

  return <div style={style}>{text}</div>;
}


export const SettingsButton = ({onClick, text}) => {
  const divStyle = {
    textAlign: 'right',
    marginTop: SIZE_SPACER_HEIGHT,
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

export class SettingInputBox extends React.Component {
  constructor(props) {
    super(props);
    this.state = {value: props.value || ''};

    this.buttonLabel = props.buttonLabel;
    this.kind = props.kind || "text";
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
      <input style={input_style} type={this.kind} value={this.state.value} onChange={this.handleChange} />
      <SettingsButton onClick={this.handleSubmit} text={this.buttonLabel} />
   </div>;
  }
}

export class SettingPasswordBox extends React.Component {
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
      <input style={input_style} type="password" value={this.state.value} onChange={this.handleChange} />
      <input style={input_style} type="password" value={this.state.value} onChange={this.handleChange} />
      <div>
        <SettingsButton onClick={this.handleSubmit} text={this.props.buttonLabel} />
      </div>
   </div>;
  }
}
